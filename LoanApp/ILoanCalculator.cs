namespace LoanApp;

public interface ILoanCalculator
{
    decimal MonthlyPayment(LoanRequest request);
}