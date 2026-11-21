namespace LoanApp;

public class SimpleCalculator : ILoanCalculator
{
    public decimal MonthlyPayment(LoanRequest request)
    {
        return request.Amount / request.Months;
    }
}