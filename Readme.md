
Connection string tenant naming convention:

{tenant/alias}_{name/dbType}

Remove the {} and replace the tenant and connectionstring with the real values.

Example:

```
{
  "ConnectionStrings": {
    "tenant1_DefaultConnection": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "alias1_DefaultConnection": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "alias2_DefaultConnection": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;
    "tenant1_SecondConnection": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=second1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;
    Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "tenant1_a.site.example.townsuite.com": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "tenant2_DefaultConnection": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase2;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
  TenantSettings{
    "DatabaseWithUniqueId": "Web",
    "SqlUniqueIdLookup": "SELECT Top 1 Id FROM ExampleTable"
  }
}
```

 The UniqueIdDbPattern will be used to compare against {tenant/alias}.  The current implementation assummed with the unique id of a tenant is stored in one of the databases. 
