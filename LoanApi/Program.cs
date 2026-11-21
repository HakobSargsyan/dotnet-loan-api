using LoanApp;
using LoanApi;
using Microsoft.EntityFrameworkCore;
using Anthropic;
using Microsoft.Extensions.AI;

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
    new AnthropicClient { ApiKey = anthropicKey }.AsIChatClient("claude-haiku-5-5"));

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

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


app.Run();
