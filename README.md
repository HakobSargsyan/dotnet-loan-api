# .NET Loan API: learning C# from a Python/PHP background

A small loan-calculation project I built while learning C# and ASP.NET Core (.NET 8), coming from Python and PHP. Each part was written by hand, chapter by chapter, and grows from a console app into a REST API backed by a database.

## What's inside

| Project | What it shows |
|---|---|
| `LoanApp` | C# fundamentals: classes, interfaces, records, LINQ, async/await, exceptions, null safety |
| `LoanApp.Tests` | xUnit tests (Arrange / Act / Assert, `Assert.Throws`) |
| `LoanApi` | ASP.NET Core Minimal API: validation with ProblemDetails, dependency injection, configuration, structured logging, EF Core + SQLite CRUD |

## Highlights

- **`ILoanCalculator`** with two implementations: simple division and the bank-style **annuity** formula, using `decimal` for money.
- **Async credit-bureau simulation**: sequential calls (~3 s) vs `Task.WhenAll` (~1 s), with partial-failure handling.
- **`POST /quote`**: returns monthly payments from every registered calculator; bad input returns `400` with all validation errors at once.
- **`/loans` CRUD**: `POST` (201), `GET` (200 / 404), `DELETE` (204 / 404) with EF Core.
- **Config**: the annual rate comes from `appsettings.json` and the app fails fast at startup if it's missing; secrets stay in user-secrets, never in Git.

## Run it

Requires the .NET 8 SDK.

```bash
dotnet test                      # run the unit tests
dotnet run --project LoanApp     # console demos
dotnet run --project LoanApi     # API, then open /swagger
```

Example request:

```bash
curl -X POST http://localhost:5062/quote \
  -H "Content-Type: application/json" \
  -d '{"customerId":"C1","amount":5000,"months":12}'
```

## Lessons learned

A few things that surprised me along the way:

- Per-call values belong in local variables, not fields: a singleton is shared by all concurrent requests.
- `^` is XOR in C#, not power, and `Math.Pow` is `double`-only.
- A record's `with` copies the object without calling the constructor, so constructor validation can be skipped.
- A setting that exists only in `appsettings.Development.json` silently becomes `0` in production.
