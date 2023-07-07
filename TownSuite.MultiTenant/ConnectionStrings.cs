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
        SqlConnectionStringBuilder csb;

        try
        {
            if (IsBase64String(cnStr ?? ""))
            {
                return Decrypt(cnStr ?? "");
            }
            csb = new SqlConnectionStringBuilder(cnStr);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
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
            Console.Error.Write(ex);
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
            Console.Error.Write(ex);
        }

        return csb.ConnectionString;
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
}