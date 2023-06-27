namespace TownSuite.MultiTenant.Tests;

public class FakeHttpClient : TsWebClient
{
    public FakeHttpClient(HttpClient httpClient, string bearerToken, string userAgent) : base(httpClient, bearerToken,
        userAgent)
    {
    }

    public override Task<ICollection<WebSearchResponse>> GetAsync(string url, CancellationToken cancellationToken)
    {
        var response = new List<WebSearchResponse>();

        var tenant1 = new WebSearchResponse()
        {
            TenantId = "tenant1",
            Connections = new List<KeyValuePairOfStringAndString>(),
            AppSettings = new List<KeyValuePairOfStringAndString>()
        };
        tenant1.Connections.Add(new KeyValuePairOfStringAndString()
        {
            Key="app1",
            Value = "PLACEHOLDER1"
        });
        tenant1.Connections.Add(new KeyValuePairOfStringAndString()
        {
            Key="app2",
            Value = "PLACEHOLDER2"
        });
        tenant1.Connections.Add(new KeyValuePairOfStringAndString()
        {
            Key="a.dns.record.as.tenant.townsuite.com_app1",
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
            Key="app1",
            Value = "PLACEHOLDER3"
        });
        tenant2.Connections.Add(new KeyValuePairOfStringAndString()
        {
            Key="app2",
            Value = "PLACEHOLDER4"
        });
        tenant2.Connections.Add(new KeyValuePairOfStringAndString()
        {
            Key="second.dns.record.as.tenant.townsuite.com_app1",
            Value = "tenant 2 alias"
        });
        
        response.Add(tenant1);
        response.Add(tenant2);

        return Task.FromResult(response as ICollection<WebSearchResponse>);
    }
}