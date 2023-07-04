

# nuget package

Build the project in Release mode.  It will produce a nuget package in the bin folder.  Upload it to your nuget repository or point the nuget source at the folder.  Have fun.

```powershell
dotnet add package "TownSuite.MultiTenant" --source "C:\the\folder\with\the\nuget\package\TownSuite.MultiTenant.nupkg"
```

# Data Formats

Connection string tenant naming convention:

{tenant/alias}_{name/dbType}

Remove the {} and replace the tenant and connectionstring with the real values.




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
    "DatabaseWithUniqueId": ".*_Web",
    "SqlUniqueIdLookup": "SELECT Top 1 Id FROM ExampleTable"
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
    private readonly ILogger<AppSettingsConfigReader> _loggerConfigReader;
    private ILogger<TenantResolver> _loggerTenantResolver;
    private readonly IConfiguration _config;

    public ExampleController(ILogger<TownSuite.MultiTenant.AppSettingsConfigReader> loggerConfigReader,
        ILogger<TenantResolver> loggerTenantResolver,
        IConfiguration config)
    {
        _loggerConfigReader = loggerConfigReader;
        _config = config;
    }

    [HttpGet()]
    public async Task<IActionResult> Get(string tenantId)
    {
        #region ThisRegionShouldBeInStartup

        // Inject TenantResolver through the constructor.
        var reader = new AppSettingsConfigReader(_config, _loggerConfigReader,
            new UniqueIdRetriever("SELECT uniqueId FROM exampleTable1"));
        if (!reader.IsSetup())
        {
            await reader.Refresh();
        }
        
        var resolver = new TenantResolver(_loggerTenantResolver, reader);
        #endregion

        var tenant = await resolver.Resolve(tenantId);

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
{
  "TenantSettings": {
    "DatabaseWithUniqueId": ".*_Web",
    "SqlUniqueIdLookup": "SELECT Top 1 Id FROM ExampleTable",
    "ConfigReaderUrl": [
      "http://localhost:5000/api/ConfigReader",
      "https://localhost:5001/api/ConfigReader"
    ],
    "ConfigReaderUrlBearerToken": "PLACEHOLDER"
  }
}


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
    private readonly ILogger<HttpConfigReader> _loggerConfigReader;
    private ILogger<TenantResolver> _loggerTenantResolver;
    private readonly IConfiguration _config;

    public ExampleController(ILogger<TownSuite.MultiTenant.HttpConfigReader> loggerConfigReader,
        ILogger<TenantResolver> loggerTenantResolver,
        IConfiguration config)
    {
        _loggerConfigReader = loggerConfigReader;
        _config = config;
    }

    [HttpGet()]
    public async Task<IActionResult> Get(string tenantId)
    {
        #region ThisRegionShouldBeInStartup
        // Inject TenantResolver through the constructor.
        var webClient = new TsWebClient(new HttpClient(),
            bearerToken: "PLACEHOLDER", userAgent: "PLACEHOLDER");
        var reader = new HttpConfigReader(_config, _loggerConfigReader,
            new UniqueIdRetriever("SELECT uniqueId FROM exampleTable1"),
            webClient);
        if (!reader.IsSetup())
        {
            await reader.Refresh();
        }

        var resolver = new TenantResolver(_loggerTenantResolver, reader);
        #endregion

        var tenant = await resolver.Resolve(tenantId);

        await using var conn = new SqlConnection(tenant.Connections["app1"]);
        await conn.OpenAsync();
        var data = await conn.QueryAsync("SELECT * FROM exampleTable2");

        return Ok(data);
    }
}
```

