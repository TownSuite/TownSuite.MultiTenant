using System.Security.Cryptography;
using System.Text;

namespace TownSuite.MultiTenant.Tests;

[TestFixture]
public class ConnectionStringsTest
{
    [Test]
    public void PlainTextConnectionStringTest()
    {
        var con = new ConnectionStrings("")
        {
            ConnStr = "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;",
            Name = "app1"
        };

        Assert.That(con.ConnStr,
            Is.EqualTo(
                "Data Source=myServerAddress;Initial Catalog=myDataBase;User ID=myUsername;Password=myPassword"));
        Assert.That(con.Name, Is.EqualTo("app1"));
    }
    
    [Test]
    public void OldSqlClientConnectionStringTest()
    {
        
        /*
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
        
        var con = new ConnectionStrings("")
        {
            ConnStr = "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;TrustServerCertificate=True",
            Name = "app1"
        };

        Assert.That(con.ConnStr,
            Is.EqualTo(
                "Data Source=myServerAddress;Initial Catalog=myDataBase;User ID=myUsername;Password=myPassword;TrustServerCertificate=True"));
        Assert.That(con.Name, Is.EqualTo("app1"));
    }
    
    [Test]
    public void NewSqlClientConnectionStringTest()
    {
        
        /*
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
        
        var con = new ConnectionStrings("")
        {
            ConnStr = "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;Trust Server Certificate=True",
            Name = "app1"
        };

        Assert.That(con.ConnStr,
            Is.EqualTo(
                "Data Source=myServerAddress;Initial Catalog=myDataBase;User ID=myUsername;Password=myPassword;Trust Server Certificate=True"));
        Assert.That(con.Name, Is.EqualTo("app1"));
    }

    [Test]
    public void EncryptedUsernameAndPasswordConnectionStringTest()
    {
        string username = Encrypt("myUsername", "abc");
        string password = Encrypt("myPassword", "abc");
        var con = new ConnectionStrings("abc")
        {
            ConnStr = $"Server=myServerAddress;Database=myDataBase;User Id={username};Password={password};",
            Name = "app1"
        };

        Assert.That(con.ConnStr,
            Is.EqualTo(
                "Data Source=myServerAddress;Initial Catalog=myDataBase;User ID=myUsername;Password=myPassword"));
        Assert.That(con.Name, Is.EqualTo("app1"));
    }

    [Test]
    public void EncryptedFullConnectionStringTest()
    {
        string cnStr = Encrypt("Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;",
            "abc");
        var con = new ConnectionStrings("abc")
        {
            ConnStr = cnStr,
            Name = "app1"
        };

        Assert.That(con.ConnStr,
            Is.EqualTo(
                "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;"));
        Assert.That(con.Name, Is.EqualTo("app1"));
    }

    private string Encrypt(string toEncrypt, string key)
    {
        if (string.IsNullOrEmpty(toEncrypt)) return string.Empty;
        byte[] keyArray = null;
        byte[] toEncryptArray = Encoding.UTF8.GetBytes(toEncrypt);

        using var hashmd5 = MD5.Create();
        keyArray = hashmd5.ComputeHash(Encoding.UTF8.GetBytes(key));
        hashmd5.Clear();

        using var tdes = TripleDES.Create();
        tdes.Key = keyArray;
        tdes.Mode = CipherMode.ECB;
        tdes.Padding = PaddingMode.PKCS7;

        using ICryptoTransform cTransform = tdes.CreateEncryptor();
        byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
        tdes.Clear();
        return Convert.ToBase64String(resultArray, 0, resultArray.Length);
    }
}