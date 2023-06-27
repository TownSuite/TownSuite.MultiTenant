
Connection string tenant naming convention:

{tenant/alias}_{name/dbType}

Remove the {} and replace the tenant and connectionstring with the real values.

Example:

```
{
  "ConnectionStrings": {
    "tenant1_app1": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "alias1_app1": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "alias2_app1": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;
    "tenant1_app2": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=second1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;
    Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "a.site.example.townsuite.com_app1": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase1;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "tenant2_app1": "Server=tcp:myserver.example.townsuite.com,1433;Initial Catalog=mydatabase2;Persist Security Info=False;User ID=myuser;Password=mypassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
  TenantSettings{
    "DatabaseWithUniqueId": ".*_Web",
    "SqlUniqueIdLookup": "SELECT Top 1 Id FROM ExampleTable"
  }
}
```

 The UniqueIdDbPattern will be used to compare against {tenant/alias}.  The current implementation assummed with the unique id of a tenant is stored in one of the databases. 
