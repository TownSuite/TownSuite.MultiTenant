using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Testcontainers.MsSql;
using TownSuite.MultiTenant;

namespace TownSuite.MultiTenant.IntegrationTests;

/// <summary>
/// End-to-end tests against a real SQL Server in a container. These validate the
/// things unit tests structurally cannot: that the library's connection-string
/// decryption produces a string SQL Server actually opens, that the reference
/// UniqueIdRetriever's lookup + scalar mapping work, and the full
/// decrypt → resolve → open → query path.
///
/// Requires Docker. Categorised as "Integration" so it can be filtered out
/// (e.g. dotnet test --filter Category!=Integration).
/// </summary>
[TestFixture]
[Category("Integration")]
public class SqlServerIntegration_Tests
{
    private const string DecryptionKey = "integration-test-key";
    private const string UniqueId = "tenant-acme";

    private readonly MsSqlContainer _sql =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    [OneTimeSetUp]
    public async Task StartContainerAndSeed()
    {
        await _sql.StartAsync();

        await using var cn = new SqlConnection(_sql.GetConnectionString());
        await cn.OpenAsync();
        await Exec(cn, "CREATE TABLE dbo.TenantInfo (UniqueId NVARCHAR(100) NOT NULL);");
        await Exec(cn, $"INSERT INTO dbo.TenantInfo (UniqueId) VALUES ('{UniqueId}');");
        await Exec(cn, "CREATE TABLE dbo.Widget (Name NVARCHAR(50) NOT NULL);");
        await Exec(cn, "INSERT INTO dbo.Widget (Name) VALUES ('gadget');");
    }

    [OneTimeTearDown]
    public async Task StopContainer() => await _sql.DisposeAsync();

    [Test]
    public async Task EncryptedConnectionString_Decrypts_Opens_Resolves_AndQueries()
    {
        // Encrypt the real container connection string with the library's scheme,
        // then feed it through config exactly as a deployment would.
        var encrypted = Encrypt(_sql.GetConnectionString(), DecryptionKey);

        var settings = BuildSettings("SELECT TOP 1 UniqueId FROM dbo.TenantInfo");
        var config = ConnectionConfig("acme_app1", encrypted);

        var reader = new AppSettingsConfigReader(config, NullLogger<AppSettingsConfigReader>.Instance,
            new UniqueIdRetriever(), settings);
        await reader.Refresh();

        // No decrypt/open/query failures, and the tenant id came from the real DB.
        Assert.That(reader.LastLoadErrorCount, Is.EqualTo(0));
        Assert.That(reader.GetTenantIds(), Does.Contain(UniqueId));

        var resolver = new TenantResolver(NullLogger<TenantResolver>.Instance, reader);
        var tenant = (await resolver.ResolveAsync(UniqueId))!;

        // The decrypted connection string actually opens and queries.
        await using var conn = tenant.CreateConnection("app1");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Name FROM dbo.Widget";
        var name = (string?)await cmd.ExecuteScalarAsync();

        Assert.That(name, Is.EqualTo("gadget"));
    }

    [Test]
    public async Task UniqueIdRetriever_RunsLookupAgainstRealDatabase()
    {
        var con = new ConnectionStrings(DecryptionKey)
        {
            Name = "acme_app1",
            ConnStr = _sql.GetConnectionString() // plaintext; opens directly
        };
        var configPairs = BuildSettings("SELECT TOP 1 UniqueId FROM dbo.TenantInfo").ConfigPairs[0];

        var id = await new UniqueIdRetriever().GetUniqueId(con, configPairs);

        Assert.That(id, Is.EqualTo(UniqueId));
    }

    [Test]
    public async Task EmptyLookupResult_IsReportedAsLoadError()
    {
        var settings = BuildSettings("SELECT TOP 1 UniqueId FROM dbo.TenantInfo WHERE 1 = 0");
        var config = ConnectionConfig("acme_app1", _sql.GetConnectionString());

        var reader = new AppSettingsConfigReader(config, NullLogger<AppSettingsConfigReader>.Instance,
            new UniqueIdRetriever(), settings);
        await reader.Refresh();

        Assert.That(reader.LastLoadErrorCount, Is.GreaterThan(0));
        Assert.That(reader.IsSetup(), Is.False);
    }

    private static Settings BuildSettings(string lookup) => new()
    {
        UserAgent = "integration",
        ConfigPairs =
        [
            new AppSettingsConfigPairs
            {
                DecryptionKey = DecryptionKey,
                UniqueIdPattern = ".*_app1",
                UniqueIdLookup = lookup
            }
        ]
    };

    private static IConfiguration ConnectionConfig(string name, string value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{name}"] = value
            })
            .Build();

    private static async Task Exec(SqlConnection cn, string sql)
    {
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    // Mirrors the library's legacy whole-string encryption (TripleDES/ECB/MD5).
    private static string Encrypt(string plaintext, string key)
    {
        using var md5 = MD5.Create();
        var keyArray = md5.ComputeHash(Encoding.UTF8.GetBytes(key));

        using var tdes = TripleDES.Create();
        tdes.Key = keyArray;
        tdes.Mode = CipherMode.ECB;
        tdes.Padding = PaddingMode.PKCS7;

        using var encryptor = tdes.CreateEncryptor();
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        return Convert.ToBase64String(encryptor.TransformFinalBlock(bytes, 0, bytes.Length));
    }
}
