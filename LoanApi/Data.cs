using Microsoft.EntityFrameworkCore;

namespace LoanApi;

public class Loan
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = "";
    public decimal Amount { get; set; }
    public int Months { get; set; }
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Loan> Loans => Set<Loan>();
}