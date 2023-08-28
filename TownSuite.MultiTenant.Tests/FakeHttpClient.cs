namespace TownSuite.MultiTenant.Tests;

public class FakeHttpClient : TsWebClient
{
    public FakeHttpClient(HttpClient httpClient, string userAgent) : base(httpClient,
        userAgent)
    {
    }

    public override Task<ICollection<WebSearchResponse>> GetAsync(string url, string bearerToken,
        CancellationToken cancellationToken)
    {
        var response = new List<WebSearchResponse>();

        if (url.Contains("//localhost:500"))
        {
            var tenant1 = new WebSearchResponse()
            {
                TenantId = "tenant1",
                Connections = new List<KeyValuePairOfStringAndString>(),
                AppSettings = new List<KeyValuePairOfStringAndString>()
            };
            tenant1.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "tenant1_app1",
                Value = "PLACEHOLDER1"
            });
            tenant1.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "tenant1_app2",
                Value = "PLACEHOLDER2"
            });
            tenant1.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "a.dns.record.as.tenant.townsuite.com_app1",
                Value = "tenant 1 alias"
            });
            var tenant2 = new WebSearchResponse()
            {
                TenantId = "tenant2",
                Connections = new List<KeyValuePairOfStringAndString>(),
                AppSettings = new List<KeyValuePairOfStringAndString>()
            };
            tenant2.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "tenant2_app1",
                Value = "PLACEHOLDER3"
            });
            tenant2.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "tenant2_app2",
                Value = "PLACEHOLDER4"
            });
            tenant2.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "second.dns.record.as.tenant.townsuite.com_app1",
                Value = "tenant 2 alias"
            });
            var tenant3 = new WebSearchResponse()
            {
                TenantId = "tenant3",
                Connections = new List<KeyValuePairOfStringAndString>(),
                AppSettings = new List<KeyValuePairOfStringAndString>()
            };
            tenant3.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "tenant3_app1",
                Value = "PLACEHOLDER5"
            });

            response.Add(tenant1);
            response.Add(tenant2);
            response.Add(tenant3);
        }
        else if (url.Contains("//localhost:600"))
        {
            var tenant4 = new WebSearchResponse()
            {
                TenantId = "tenant4",
                Connections = new List<KeyValuePairOfStringAndString>(),
                AppSettings = new List<KeyValuePairOfStringAndString>()
            };
            tenant4.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "tenant4_app1",
                Value = "PLACEHOLDER6"
            });
            tenant4.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "tenant4_app2",
                Value = "PLACEHOLDER7"
            });
            tenant4.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "fourth.dns.record.as.tenant.townsuite.com_app4",
                Value = "tenant 4 alias"
            });
            var tenant5 = new WebSearchResponse()
            {
                TenantId = "tenant5",
                Connections = new List<KeyValuePairOfStringAndString>(),
                AppSettings = new List<KeyValuePairOfStringAndString>()
            };
            tenant5.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "tenant5_app1",
                Value = "PLACEHOLDER8"
            });
            tenant5.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "tenant5_app2",
                Value = "PLACEHOLDER9"
            });
            tenant5.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "fifth.dns.record.as.tenant.townsuite.com_app1",
                Value = "tenant 5 alias"
            });
            var tenant6 = new WebSearchResponse()
            {
                TenantId = "tenant6",
                Connections = new List<KeyValuePairOfStringAndString>(),
                AppSettings = new List<KeyValuePairOfStringAndString>()
            };
            tenant6.Connections.Add(new KeyValuePairOfStringAndString()
            {
                Key = "tenant6_app1",
                Value = "PLACEHOLDER10"
            });

            response.Add(tenant4);
            response.Add(tenant5);
            response.Add(tenant6);
        }


        return Task.FromResult(response as ICollection<WebSearchResponse>);
    }
}