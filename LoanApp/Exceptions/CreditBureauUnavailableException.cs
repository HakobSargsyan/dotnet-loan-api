public class CreditBureauUnavailableException : Exception
{
    public CreditBureauUnavailableException(string customerId)
        : base($"Credit bureau unavailable for {customerId}") { }
}