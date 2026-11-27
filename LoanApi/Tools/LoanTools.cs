namespace LoanApi;
using System.ComponentModel;
using LoanApp;

public class LoanTools {
    private readonly ILogger<LoanTools> _logger;

    public LoanTools(ILogger<LoanTools> logger)
    {
        _logger = logger;
    }

    [Description("Calculate monthly payment based monthly pay and amount size")]
    public string MonthlyPayment(
        [Description("Loan amount in Armenian dram (AMD)")] decimal amount,
        [Description("Annual interest rate in percent, e.g. 9.5. Take it from SearchProducts, never guess")] decimal annualRatePercent,
        [Description("Repayment period in months; convert years to months")] int months){

        if (amount <= 0 || amount > 50_000_000m) return "Error: amount must be between 1 and 50,000,000 AMD.";
        if (months < 1 || months > 360) return "Error: months must be between 1 and 360.";
        if (annualRatePercent <= 0 || annualRatePercent > 100) return "Error: annualRatePercent must be above 0 and at most 100.";

        var calculator = new AnnuityCalculator(annualRatePercent / 100m);

        var request = new LoanRequest("chat", amount, months);

        var result = Math.Round(calculator.MonthlyPayment(request), 2);

        _logger.LogInformation("MonthlyPayment amount={Amount} rate={Rate} months={Months} result={Result}", amount, annualRatePercent, months, result);

        return $"{result} AMD";

    }
}