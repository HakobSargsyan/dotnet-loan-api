using LoanApp;
using LoanApi;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

decimal rate = builder.Configuration.GetValue<decimal?>("Loans:AnnualRate") ?? throw new InvalidOperationException("Config 'Loans:AnnualRate' is missing");
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite("Data Source=loans.db"));
builder.Services.AddSingleton<ILoanCalculator, SimpleCalculator>();
builder.Services.AddSingleton<ILoanCalculator>(_ => new AnnuityCalculator(rate));


var app = builder.Build();
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

Console.WriteLine($"Key loaded: {app.Configuration["OpenAI:ApiKey"] is not null}");
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


app.Run();
