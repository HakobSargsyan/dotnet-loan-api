namespace LoanApi;
using System.ComponentModel;
using LoanApp;

public class LoanTools {
    public decimal MonthlyPayment(
        [Description("Loan amount in Armenian dram (AMD)")] decimal amount,
        [Description("Annual interest rate in percent, e.g. 9.5. Take it from SearchProducts, never guess")] decimal annualRatePercent,
        [Description("Repayment period in months; convert years to months")] int months){

        var calculator = new AnnuityCalculator(annualRatePercent / 100m);

        var request = new LoanRequest("chat", amount, months);

        //Console.WriteLine($"[tool] MonthlyPayment(amount={amount}, months={months}) = {calc}");

        return calculator.MonthlyPayment(request);
    }
}