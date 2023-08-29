using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace TownSuite.MultiTenant;

public class ConnectionStrings
{
    private readonly string _decryptionKey;

    public ConnectionStrings(string decryptionKey)
    {
        _decryptionKey = decryptionKey;
    }

    public string Name { get; init; }
    private string _connStr;

    public string ConnStr
    {
        get => _connStr;
        init { _connStr = DeCryptConnectionString(value); }
    }

    public void ChangeConnStr(string connStr)
    {
        _connStr = connStr;
    }
    
    private string DeCryptConnectionString(
        string cnStr)
    {
        
        bool isOldStyle = !IsMicrosoftDataConnectionString(cnStr);
        
        SqlConnectionStringBuilder csb;

        try
        {
            if (IsBase64String(cnStr ?? ""))
            {
                return isOldStyle ? RevertToSystemDataSqlClientCompatibleConnectionString(Decrypt(cnStr ?? "")) : Decrypt(cnStr ?? "");
            }
            csb = new SqlConnectionStringBuilder(cnStr);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Warning, Connection string is not encrypted.");
            return cnStr;
        }

        try
        {
            bool hasPass = csb.TryGetValue("password", out object encryptedPassword);
            if (hasPass && IsBase64String(encryptedPassword?.ToString() ?? ""))
            {
                csb["password"] = Decrypt(encryptedPassword?.ToString());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Warning, Connection string password is not encrypted.");
        }

        try
        {
            bool hasUsername = csb.TryGetValue("User Id", out object encryptedUsername);
            if (hasUsername && IsBase64String(encryptedUsername?.ToString() ?? ""))
            {
                csb["User Id"] = Decrypt(encryptedUsername?.ToString());
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(new TownSuiteException("Connection string userid is not encrypted.", ex));
        }

        return isOldStyle ? RevertToSystemDataSqlClientCompatibleConnectionString(csb.ConnectionString) : csb.ConnectionString;
    }

    private static bool IsBase64String(string base64)
    {
        Span<byte> buffer = new Span<byte>(new byte[base64.Length]);
        return Convert.TryFromBase64String(base64, buffer, out int bytesParsed);
    }

    private string Decrypt(string cipherString)
    {
        if (string.IsNullOrEmpty(cipherString)) return string.Empty;
        byte[] keyArray = null;

        byte[] toEncryptArray = Convert.FromBase64String(cipherString);

        using var hashmd5 = MD5.Create();
        keyArray = hashmd5.ComputeHash(Encoding.UTF8.GetBytes(_decryptionKey));
        hashmd5.Clear();

        using var tdes = TripleDES.Create();
        tdes.Key = keyArray;
        tdes.Mode = CipherMode.ECB;
        tdes.Padding = PaddingMode.PKCS7;

        using ICryptoTransform cTransform = tdes.CreateDecryptor();
        byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
        tdes.Clear();
        return Encoding.UTF8.GetString(resultArray);
    }

    private string RevertToSystemDataSqlClientCompatibleConnectionString(string connectionString)
    {
        var sb = new StringBuilder();
        sb.Append(connectionString);
        sb.Replace("Application Intent", "ApplicationIntent");
        sb.Replace("Connect Retry Count", "ConnectRetryCount");
        sb.Replace("Connect Retry Interval", "ConnectRetryInterval");
        sb.Replace("Pool Blocking Period", "PoolBlockingPeriod");
        sb.Replace("Multiple Active Result Sets", "MultipleActiveResultSets");
        sb.Replace("Multiple Subnet Failover", "MultiSubnetFailover");
        sb.Replace("Transparent Network IP Resolution", "TransparentNetworkIPResolution");
        sb.Replace("Trust Server Certificate", "TrustServerCertificate");
        return sb.ToString();
    }
    
    private bool IsMicrosoftDataConnectionString(string cnStr)
    {
        /*
        ConnectionStringDbType valid types are microsoft.data.sqlclient and system.data.sqlclient
        specifying microsoft.data.sqlclient will return connection strings that break when using system.data.sqlclient code.
        see https://github.com/dotnet/SqlClient/issues/1780 and https://github.com/dotnet/SqlClient/pull/534

        Application Intent (previously ApplicationIntent)
        Connect Retry Count (previously ConnectRetryCount)
        Connect Retry Interval (previously ConnectRetryInterval)
        Pool Blocking Period (previously PoolBlockingPeriod)
        Multiple Active Result Sets (previously MultipleActiveResultSets)
        Multiple Subnet Failover (previously MultiSubnetFailover)
        Transparent Network IP Resolution (previously TransparentNetworkIPResolution)
        Trust Server Certificate (previously TrustServerCertificate)
         */

        if (cnStr.Contains("ApplicationIntent", StringComparison.OrdinalIgnoreCase)
            || cnStr.Contains("ConnectRetryCount", StringComparison.OrdinalIgnoreCase)
            || cnStr.Contains("ConnectRetryInterval", StringComparison.OrdinalIgnoreCase)
            || cnStr.Contains("PoolBlockingPeriod", StringComparison.OrdinalIgnoreCase)
            || cnStr.Contains("MultipleActiveResultSets", StringComparison.OrdinalIgnoreCase)
            || cnStr.Contains("MultiSubnetFailover", StringComparison.OrdinalIgnoreCase)
            || cnStr.Contains("TransparentNetworkIPResolution", StringComparison.OrdinalIgnoreCase)
            || cnStr.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase)
           )
        {
            // is an older style connection string that can work with system.data.sqlclient
            return false;
        }

        // is a newer style connection string that can work with microsoft.data.sqlclient
        return true;
    }
}