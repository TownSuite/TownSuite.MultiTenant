namespace TownSuite.MultiTenant;

public class TownSuiteException : Exception
{
    public TownSuiteException(string message) : base(message)
    {
    }

    public TownSuiteException(string message, Exception innerException) : base(message, innerException)
    {
    }
}