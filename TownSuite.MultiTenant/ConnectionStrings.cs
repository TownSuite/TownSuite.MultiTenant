using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace TownSuite.MultiTenant;

public class ConnectionStrings
{
    /// <summary>
    /// Optional explicit marker for a fully-encrypted connection string. When a
    /// value starts with this prefix the remainder is always treated as
    /// ciphertext, avoiding the base64 false-positive heuristic. Values without
    /// the prefix continue to use the legacy auto-detection for backwards
    /// compatibility.
    /// </summary>
    public const string EncryptionPrefix = "enc:";

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

    /// <summary>
    /// The tenant or alias portion of <see cref="Name"/> (text before the first '_').
    /// </summary>
    public string TenantOrAlias => Name?.Split('_').FirstOrDefault();

    /// <summary>
    /// The application type portion of <see cref="Name"/> (text after the last '_').
    /// </summary>
    public string AppType => Name?.Split('_').LastOrDefault();

    /// <summary>
    /// Replaces the stored connection string with an already-decrypted value.
    /// Unlike the <see cref="ConnStr"/> initializer, this does NOT run
    /// decryption — pass a plaintext/decrypted connection string only.
    /// </summary>
    public void SetDecryptedConnStr(string decryptedConnStr)
    {
        _connStr = decryptedConnStr;
    }

    private string DeCryptConnectionString(string cnStr)
    {
        string raw = cnStr ?? "";
        bool explicitlyEncrypted = raw.StartsWith(EncryptionPrefix, StringComparison.Ordinal);
        if (explicitlyEncrypted)
        {
            raw = raw.Substring(EncryptionPrefix.Length);
        }

        bool isOldStyle = !IsMicrosoftDataConnectionString(raw);

        if (explicitlyEncrypted)
        {
            // The caller explicitly marked this value as encrypted, so a failed
            // decrypt is a hard error rather than something to silently pass
            // through as plaintext (which would only surface as a confusing SQL
            // error later).
            try
            {
                var decrypted = Decrypt(raw);
                return isOldStyle
                    ? RevertToSystemDataSqlClientCompatibleConnectionString(decrypted)
                    : decrypted;
            }
            catch (Exception ex)
            {
                throw new TownSuiteException(
                    $"Failed to decrypt connection string '{Name}' marked with the '{EncryptionPrefix}' prefix.",
                    ex);
            }
        }

        SqlConnectionStringBuilder csb;

        try
        {
            if (IsBase64String(raw))
            {
                return isOldStyle
                    ? RevertToSystemDataSqlClientCompatibleConnectionString(Decrypt(raw))
                    : Decrypt(raw);
            }

            csb = new SqlConnectionStringBuilder(raw);
        }
        catch (Exception)
        {
            // The value is not an encrypted/parseable connection string; treat it
            // as plaintext and return it unchanged.
            return raw;
        }

        try
        {
            bool hasPass = csb.TryGetValue("password", out object encryptedPassword);
            if (hasPass && IsBase64String(encryptedPassword?.ToString() ?? ""))
            {
                csb["password"] = Decrypt(encryptedPassword?.ToString());
            }
        }
        catch (Exception)
        {
            // Password segment is not encrypted; leave it as-is.
        }

        try
        {
            bool hasUsername = csb.TryGetValue("User Id", out object encryptedUsername);
            if (hasUsername && IsBase64String(encryptedUsername?.ToString() ?? ""))
            {
                csb["User Id"] = Decrypt(encryptedUsername?.ToString());
            }
        }
        catch (Exception)
        {
            // User id segment is not encrypted; leave it as-is.
        }

        return isOldStyle
            ? RevertToSystemDataSqlClientCompatibleConnectionString(csb.ConnectionString)
            : csb.ConnectionString;
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
