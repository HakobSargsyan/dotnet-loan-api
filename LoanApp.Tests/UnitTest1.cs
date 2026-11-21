namespace LoanApp.Tests;

public class UnitTest1
{
    [Fact]
    public void SimpleCalculator_5000_12Months()
    {
        var simpleCalc = new SimpleCalculator();

        var d = new LoanRequest("1", 5000);

        decimal p = simpleCalc.MonthlyPayment(d);

        Assert.Equal(416.67m, Math.Round(p, 2));
    }

    [Fact]
    public void AnnuityCalculator_5000_12Months_12Percent()
    {
        var annuityCalc = new AnnuityCalculator(0.12m);

        var d = new LoanRequest("1", 5000);

        decimal p = annuityCalc.MonthlyPayment(d);

        Assert.Equal(444.24m, Math.Round(p, 2));
    }

    [Fact]
    public void LoanRequest_ZeroMonths_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoanRequest("1", 5000m, 0));
    }
}