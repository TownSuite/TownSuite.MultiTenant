namespace TownSuite.MultiTenant;

public class ConnectionStrings
{
    public string Name { get; init; }
    private string _connStr;

    public string ConnStr
    {
        get => _connStr;
        init => _connStr = value;
    }

    public void ChangeConnStr(string connStr)
    {
        _connStr = connStr;
    }
}