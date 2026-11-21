//namespace LoanApp;
//
//public class LoanRequest
//{
//    public string CustomerId { get; }
//    public decimal Amount { get; }
//    public int Months { get; }
//
//    public LoanRequest(string customerId, decimal amount, int months = 12)
//    {
//        CustomerId = customerId;
//        Amount = amount;
//        Months = months;
//    }
//
//    public decimal MonthlyPayment() => Amount / Months;
//}
//

namespace LoanApp;

public record LoanRequest(string CustomerId, decimal Amount, int Months = 12, string? Email = null){
    private readonly bool _isValid = Validate(Amount, Months);

    private static bool Validate(decimal Amount, int Months){
       if (Amount <= 0) throw new ArgumentOutOfRangeException(nameof(Amount));
       if (Months <= 0) throw new ArgumentOutOfRangeException(nameof(Months));
       return true;
    }
}