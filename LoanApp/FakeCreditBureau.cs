namespace LoanApp;

public class FakeCreditBureau : ICreditBureau
{
    public async Task<decimal> GetCreditScoreAsync(string customerId)
    {
        if (customerId == "C1") throw new CreditBureauUnavailableException(customerId);
        await Task.Delay(1000);
        Console.WriteLine(customerId);
        return 750m;
    }

}