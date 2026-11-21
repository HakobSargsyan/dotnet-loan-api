namespace LoanApp;

public interface ICreditBureau
{
    Task<decimal> GetCreditScoreAsync(string customerId);
}