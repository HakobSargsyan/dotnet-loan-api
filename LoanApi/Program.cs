using LoanApp;
using LoanApi;
using Microsoft.EntityFrameworkCore;
using Anthropic;
using Microsoft.Extensions.AI;
using OllamaSharp;
using System.Numerics.Tensors;

var builder = WebApplication.CreateBuilder(args);
string anthropicKey = builder.Configuration["Anthropic:ApiKey"]
    ?? throw new InvalidOperationException("Anthropic:ApiKey is not configured");

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

decimal rate = builder.Configuration.GetValue<decimal?>("Loans:AnnualRate") ?? throw new InvalidOperationException("Config 'Loans:AnnualRate' is missing");
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite("Data Source=loans.db"));
builder.Services.AddSingleton<ILoanCalculator, SimpleCalculator>();
builder.Services.AddSingleton<ILoanCalculator>(_ => new AnnuityCalculator(rate));
builder.Services.AddChatClient(
    new AnthropicClient { ApiKey = anthropicKey }.AsIChatClient("claude-haiku-5-5")).UseFunctionInvocation();
builder.Services.AddEmbeddingGenerator(
    new OllamaApiClient(new Uri("http://localhost:11434"), "nomic-embed-text"));
builder.Services.AddSingleton<ProductStore>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();


//init store
var store = app.Services.GetRequiredService<ProductStore>();
var gen = app.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
await store.IngestAsync(gen, CancellationToken.None);
// store


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/quote", (QuoteRequest req, IEnumerable<ILoanCalculator> calcs, ILogger<Program> logger) =>
{
    var results = new List<object>();
    var errors = new Dictionary<string, string[]>();

    if (req.Amount <= 0) errors["amount"] = ["Amount must be greater than 0"];
    if (req.Months <= 0) errors["month"] = ["Month must be greater than 0"];

    if (errors.Count > 0) {
        logger.LogWarning("Invalid quote request: {@Errors}", errors);
        return Results.ValidationProblem(errors);
    }
    var loan = new LoanRequest(req.CustomerId, req.Amount, req.Months);

    foreach (var calc in calcs)
    {
        decimal p = calc.MonthlyPayment(loan);
        results.Add(new
        {
            Calculator = calc.GetType().Name,
            MonthlyPayment = p
        });
    }
    logger.LogInformation("Quote for {CustomerId}: {Amount} over {Months} months", req.CustomerId, req.Amount, req.Months);
    return Results.Ok(results);
});

app.MapPost("/loans", async (Loan loan, AppDbContext db) =>
{
    db.Loans.Add(loan);
    await db.SaveChangesAsync();
    return Results.Created($"/loans/{loan.Id}", loan);
});

app.MapGet("/loans/{id}", async (int id, AppDbContext db) =>
{
    var loan = await db.Loans.FindAsync(id);
    if (loan is null) return Results.NotFound();
    return Results.Ok(loan);
});

app.MapGet("/loans", async (AppDbContext db) =>
{
    var loans = await db.Loans.AsNoTracking().ToListAsync();
    if (loans is null) return Results.NotFound();
    return Results.Ok(loans);
});

app.MapDelete("/loans/{id}", async (int id, AppDbContext db) =>
{
    var loan = await db.Loans.FindAsync(id);
    if (loan is null)
    {
        return Results.NotFound();
    }

    db.Loans.Remove(loan);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapPost("/explain", async (QuoteRequest req, IEnumerable<ILoanCalculator> calcs, IChatClient chat) =>
{
    var loan = new LoanRequest(req.CustomerId, req.Amount, req.Months);

    var annuity = calcs.OfType<AnnuityCalculator>().First();
    decimal payment = Math.Round(annuity.MonthlyPayment(loan), 2);
    decimal totalInterest = ( payment * req.Months ) - req.Amount;

    string prompt = $"""
        You are a bank assistant, writing to a customer with no finance background.
        Facts (already calculated by the bank, use them exactly, do not recalculate):
        - Loan amount: {req.Amount} AMD
        - Term: {req.Months} months
        - Annual interest rate: {rate:P0}
        - Monthly payment: {payment} AMD
        - Total interest over the loan: {totalInterest} AMD
        Task: explain this loan in short sentences.
        Rules: plain language, no markdown, do not give financial advice, do not mention numbers that are not in the facts.
        """;

    ChatResponse response = await chat.GetResponseAsync(prompt);
    return Results.Ok(new { payment, totalInterest, explanation = response.Text });
});


app.MapPost("/explain/stream", async (QuoteRequest req, IEnumerable<ILoanCalculator> calcs,
IChatClient chat, HttpResponse response, ILogger<Program> logger, CancellationToken ct) =>
{
    var results = new List<object>();
    var errors = new Dictionary<string, string[]>();

    if (req.Amount <= 0) errors["amount"] = ["Amount must be greater than 0"];
    if (req.Months <= 0) errors["month"] = ["Month must be greater than 0"];

    if (errors.Count > 0) {
        logger.LogWarning("Invalid quote request: {@Errors}", errors);
        return Results.ValidationProblem(errors);
    }

    var loan = new LoanRequest(req.CustomerId, req.Amount, req.Months);

    var annuity = calcs.OfType<AnnuityCalculator>().First();
    decimal payment = Math.Round(annuity.MonthlyPayment(loan), 2);
    decimal totalInterest = ( payment * req.Months ) - req.Amount;

    string prompt = $"""
        You are a bank assistant, writing to a customer with no finance background.
        Facts (already calculated by the bank, use them exactly, do not recalculate):
        - Loan amount: {req.Amount} AMD
        - Term: {req.Months} months
        - Annual interest rate: {rate:P0}
        - Monthly payment: {payment} AMD
        - Total interest over the loan: {totalInterest} AMD
        Task: explain this loan in short sentences.
        Rules: plain language, no markdown, do not give financial advice, do not mention numbers that are not in the facts.
        """;

    response.ContentType = "text/plain; charset=utf-8";
    await foreach (ChatResponseUpdate update in chat.GetStreamingResponseAsync(prompt, cancellationToken: ct))
    {
       await response.WriteAsync(update.Text);
       await response.Body.FlushAsync(ct);
    }

    return Results.Empty;
});


app.MapPost("/explain/structured", async (QuoteRequest req, IEnumerable<ILoanCalculator> calcs,
IChatClient chat, ILogger<Program> logger, CancellationToken ct) =>
{
    var results = new List<object>();
    var errors = new Dictionary<string, string[]>();

    if (req.Amount <= 0) errors["amount"] = ["Amount must be greater than 0"];
    if (req.Months <= 0) errors["month"] = ["Month must be greater than 0"];

    if (errors.Count > 0) {
        logger.LogWarning("Invalid quote request: {@Errors}", errors);
        return Results.ValidationProblem(errors);
    }

    var loan = new LoanRequest(req.CustomerId, req.Amount, req.Months);

    var annuity = calcs.OfType<AnnuityCalculator>().First();
    decimal payment = Math.Round(annuity.MonthlyPayment(loan), 2);
    decimal totalInterest = ( payment * req.Months ) - req.Amount;

    string prompt = $"""
        You are a bank assistant, writing to a customer with no finance background.
        Facts (already calculated by the bank, use them exactly, do not recalculate):
        - Loan amount: {req.Amount} AMD
        - Term: {req.Months} months
        - Annual interest rate: {rate:P0}
        - Monthly payment: {payment} AMD
        - Total interest over the loan: {totalInterest} AMD
        Task: explain this loan in short sentences.
        Rules: plain language, no markdown, do not give financial advice, do not mention numbers that are not in the facts.
        """;

    ChatResponse<LoanExplanation> response = await chat.GetResponseAsync<LoanExplanation>(prompt, cancellationToken: ct);
    return Results.Ok(new { payment, totalInterest, explanation = response.Result });
});


app.MapPost("/embed", async (EmbedRequest req, IEmbeddingGenerator<string, Embedding<float>> gen, ILogger<Program> logger, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            { nameof(req.Text), ["Text should be greater than 0"] }
        });
    }

    var embedding = await gen.GenerateAsync([req.Text], cancellationToken: ct);

    var vectorSize = embedding[0].Vector.Length;
    var first5 = embedding[0].Vector.Slice(0,5);
    return Results.Ok(new
    {
        dimensions = vectorSize,
        first5 = first5
    });

});

app.MapGet("/products/chunks", (ProductStore store) =>
{
   var products = store.Products.Select(chunk => new
    {
         source = chunk.Source,
         text = chunk.Text,
         dimensions = chunk.Vector.Length
    });


    return Results.Ok(products);

});


app.MapPost("/products/search", async (EmbedRequest req, ProductStore store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            { nameof(req.Text), ["Text should be greater than 0"] }
        });
    }

    var embedding = await gen.GenerateAsync([req.Text], cancellationToken: ct);

    var hits = store.Search(embedding[0].Vector);
    return Results.Ok(hits.Select(h => new { source = h.Chunk.Source, score = h.Score }));

});


app.MapPost("/ask", async (EmbedRequest req, IEmbeddingGenerator<string, Embedding<float>> gen, ProductStore store, CancellationToken ct, IChatClient chat) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            { nameof(req.Text), ["Text should be greater than 0"] }
        });
    }

    var embedding = await gen.GenerateAsync([req.Text], cancellationToken: ct);

    var hits = store.Search(embedding[0].Vector);

    var filtered = hits
        .Where(h => h.Score > 0.5)
        .ToList();

    string facts = string.Join("\n", filtered.Select(h => $"[{h.Chunk.Source}] {h.Chunk.Text}"));

    if(filtered.Count > 0) {
        string prompt = $"""
            You are a bank assistant answering a customer question.
            Facts (from the bank's product catalog):
            {facts}
            Question: {req.Text}
            Rules: answer only from the facts; if the facts do not contain the answer, say you don't know;
            cite the source in brackets like [mortgage-loan]; plain language, no markdown, no financial advice.
            """;

        ChatResponse response = await chat.GetResponseAsync(prompt, cancellationToken: ct);
        return Results.Ok(new { answer = response.Text, sources = filtered.Select(h => h.Chunk.Source) });

    }else {
        return Results.Ok(new { answer = "I can only answer questions about our bank products.", sources = Array.Empty<string>() });
    }
});


app.MapPost("/chat", async (EmbedRequest req, IEmbeddingGenerator<string, Embedding<float>> gen, ProductStore store, CancellationToken ct, IChatClient chat) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            { nameof(req.Text), ["Text should be greater than 0"] }
        });
    }

    var loanTools = new LoanTools();
    var productTools = new ProductTools(store, gen);
    var options = new ChatOptions
    {
        Tools =
        [
            AIFunctionFactory.Create(loanTools.MonthlyPayment),
            AIFunctionFactory.Create(productTools.SearchProducts)
        ]
    };



    string systemPrompt = """
        You are a bank assistant. All amounts are in AMD.
        Take interest rates only from SearchProducts, never guess them.
        Use MonthlyPayment for payments. Never calculate numbers yourself.
        Plain text, no markdown. Politely decline non-banking questions.
        """;

    List<ChatMessage> messages =
    [
        new(ChatRole.System, systemPrompt),
        new(ChatRole.User, req.Text)
    ];

    ChatResponse response = await chat.GetResponseAsync(messages, options, ct);

    var toolCalls = response.Messages
                        .SelectMany(m => m.Contents)
                        .OfType<FunctionCallContent>()
                        .Select(c => new { tool = c.Name, args = c.Arguments });

    return Results.Ok(new { answer = response.Text, toolCalls });

});



app.Run();
