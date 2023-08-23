

# nuget package

Build the project in Release mode.  It will produce a nuget package in the bin folder.  Upload it to your nuget repository or point the nuget source at the folder.  Have fun.

```powershell
dotnet add package "TownSuite.MultiTenant" --source "C:\the\folder\with\the\nuget\package\TownSuite.MultiTenant.nupkg"
```

# Data Formats

Connection string tenant naming convention:

{tenant/alias}_{name/dbType}

Remove the {} and replace the tenant and connectionstring with the real values.


## DI setup

program.cs add services

```cs
services.AddSingleton<TownSuite.MultiTenant.Settings>((s) => new TownSuite.MultiTenant.Settings()
{
    UniqueIdDbPattern = s.GetService<IConfiguration>().GetSection("TenantSettings")
        .GetSection("UniqueIdDbPattern").Value,
    DecryptionKey = s.GetService<IConfiguration>().GetSection("TenantSettings").GetSection("DecryptionKey")
        .Value,
    ConfigReaderUrls = s.GetService<IConfiguration>().GetSection("TenantSettings").GetSection("ConfigReaderUrl")
        .Get<string[]>()
});
services.AddSingleton<IUniqueIdRetriever>((s) =>
{
    var config = s.GetService<IConfiguration>();
    string sqlUniqueIdLookup =
        config.GetSection("TenantSettings").GetSection("SqlUniqueIdLookup").Get<string>();
    return new UniqueIdRetriever(sqlUniqueIdLookup);
});
services.AddSingleton<TsWebClient>((s) =>
{
    var config = s.GetService<IConfiguration>();
    string userAgent =
        config.GetSection("TenantSettings").GetSection("UserAgent").Get<string>();
    string bearerToken =
        config.GetSection("TenantSettings").GetSection("SqlUniqueIdLookup").Get<string>();
    var webClient = new TsWebClient(new HttpClient(),
        bearerToken: bearerToken, userAgent: userAgent);
    return webClient;
});
services.AddSingleton<IConfigReader, HttpConfigReader>();
services.AddSingleton<TenantResolver>();
```


## AppSettingsConfigReader - Example

Read tenant information from a appsettings.json file.

```json
{
  "ConnectionStrings": {
    "tenant1_app1": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "alias1_app1": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "alias2_app1": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;",
    "tenant1_app2": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=second1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "a.site.example.townsuite.com_app1": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "tenant2_app1": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase2;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "TenantSettings": {
    // AppSettingsConfigReader supports only 1 record in the ConfigPairs
    "ConfigPairs": [
        {
            "Id": 1,
            "DecryptionKey": "PLACEHOLDER",
            "UniqueIdDbPattern": ".*_Web",
            "SqlUniqueIdLookup": "SELECT Top 1 Id FROM ExampleTable"
        }
    ],
    "UserAgent": "TownSuite-MultiTenant-Console Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:109.0) Gecko/20100101 Firefox/115.0"
  }
}
```

The UniqueIdDbPattern will be used to compare against {tenant/alias}.  The current implementation assummed with the unique id of a tenant is stored in one of the databases. 


### asp.net core example reading settings from appsetting.json

```cs
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TownSuite.MultiTenant;

namespace ExampleApplication.Controllers;

[ApiController]
[Route("[controller]")]
public class ExampleController : ControllerBase
{
    private readonly TenantResolver _resolver;

    public ExampleController(TenantResolver resolver)
    {
        _resolver = resolver;
    }

    [HttpGet()]
    public async Task<IActionResult> Get(string tenantId)
    {
        var tenant = await _resolver.Resolve(tenantId);

        await using var conn = new SqlConnection(tenant.Connections["app1"]);
        await conn.OpenAsync();
        var data = await conn.QueryAsync("SELECT * FROM exampleTable2");

        return Ok(data);
    }
}
```

## HttpConfigReader - Example 

Settings that are required to make an http call and read the output
```json
"TenantSettings": {
    "ConfigPairs": [
        {
            "Id": 1,
            "ConfigReaderUrls": [
                "http://localhost:5000/api/ConfigReader"
            ],
            "ConfigReaderUrlBearerToken": "PLACEHOLDER",
            "DecryptionKey": "PLACEHOLDER",
            "UniqueIdDbPattern": ".*_Web",
            "SqlUniqueIdLookup": "SELECT Top 1 Id FROM ExampleTable"
        }
    ],
    "UserAgent": "TownSuite-MultiTenant-Console Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:109.0) Gecko/20100101 Firefox/115.0"
}
```

The expected data format from an http service is json that matches the below example.

```json
[
    {
        "tenantId": "tenant1",
        "connections": [
            {
                "key": "app1",
                "value": "CONNECTIONSTRING PLACEHOLDER"
            },
            {
                "key": "app2",
                "value": "CONNECTIONSTRING PLACEHOLDER"
            }
        ]
    },
    {
        "tenantId": "tenant2",
        "connections": [
            {
                "key": "app1",
                "value": "CONNECTIONSTRING PLACEHOLDER"
            },
            {
                "key": "app2",
                "value": "CONNECTIONSTRING PLACEHOLDER"
            }
        ]
    }
]
```


### asp.net core example 

```cs
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TownSuite.MultiTenant;

namespace ExampleApplication.Controllers;

[ApiController]
[Route("[controller]")]
public class ExampleController : ControllerBase
{
    private readonly TenantResolver _resolver;

    public ExampleController(TenantResolver resolver)
    {
        _resolver = resolver;
    }

    [HttpGet()]
    public async Task<IActionResult> Get(string tenantId)
    {
        var tenant = await _resolver.Resolve(tenantId);

        await using var conn = new SqlConnection(tenant.Connections["app1"]);
        await conn.OpenAsync();
        var data = await conn.QueryAsync("SELECT * FROM exampleTable2");

        return Ok(data);
    }
}
```



### Worker service connecting to all tenants


use an extension method to create connections


```cs
public static class TenantExtensions
{
    public static DbConnection CreateConnection(this Tenant tenant, string appName)
    {
        return new SqlConnection(tenant.Connections[appName]);
    }
}
```


Worker background service looping through all tenants and reading data.

```cs
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly TenantResolver _resolver;

    public Worker(ILogger<Worker> logger, TenantResolver resolver)
    {
        _logger = logger;
        _resolver= resolver;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        const int oneHour = 1000 * 60 * 60;
        
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            await _resolver.ResolveAll();

            foreach (var tenant in _resolver.Tenants)
            {
                // example: do stuff with tenants app1 databases
                await using var conn =  tenant.CreateConnection("app1");
                await conn.OpenAsync();
                var data = await conn.QueryAsync("SELECT * FROM exampleTable2");
            }

            await Task.Delay(oneHour, stoppingToken);
        }
    }
}
```

