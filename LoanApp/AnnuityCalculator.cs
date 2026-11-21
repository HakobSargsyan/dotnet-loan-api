namespace LoanApp;

public class AnnuityCalculator : ILoanCalculator
{
     private readonly decimal _annualRate;
     public AnnuityCalculator(decimal annualRate)
     {
         _annualRate = annualRate;
     }

     public decimal MonthlyPayment(LoanRequest request){
        decimal r = _annualRate / 12;

        int months = request.Months;

        decimal payment = request.Amount * r / (1m - 1m / Pow(1m + r, months));

        return payment;
     }

     public static decimal Pow(decimal baseValue, int exponent)
     {
         decimal result = 1m;

         for (int i = 0; i < exponent; i++)
         {
             result *= baseValue;
         }

         return result;
     }
}