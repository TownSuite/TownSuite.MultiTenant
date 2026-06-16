using System.Net;
using System.Text;

namespace TownSuite.MultiTenant.Tests;

public class TsWebClient_Tests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;
        public HttpRequestMessage? LastRequest;

        public StubHandler(HttpResponseMessage response)
            : this((_, _) => response)
        {
        }

        public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_responder(request, cancellationToken));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static TsWebClient Client(HttpMessageHandler handler, string userAgent = "test-agent") =>
        new(new HttpClient(handler), userAgent);

    [Test]
    public async Task GetAsync_200_DeserializesConnections()
    {
        const string body =
            """[{"tenantId":"t1","connections":[{"key":"t1_app1","value":"cs1"}],"appSettings":[]}]""";
        var client = Client(new StubHandler(Json(HttpStatusCode.OK, body)));

        var result = await client.GetAsync("http://localhost/api", "token", CancellationToken.None);

        Assert.That(result.Count, Is.EqualTo(1));
        var tenant = result.First();
        Assert.That(tenant.TenantId, Is.EqualTo("t1"));
        Assert.That(tenant.Connections.First().Key, Is.EqualTo("t1_app1"));
        Assert.That(tenant.Connections.First().Value, Is.EqualTo("cs1"));
    }

    [Test]
    public void GetAsync_NonSuccess_ThrowsApiException_WithoutLeakingBody()
    {
        const string secretBody = "Server=db;User Id=sa;Password=SuperSecret123;";
        var client = Client(new StubHandler(Json(HttpStatusCode.InternalServerError, secretBody)));

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await client.GetAsync("http://localhost/api", "token", CancellationToken.None));

        Assert.That(ex!.StatusCode, Is.EqualTo(500));
        // The body must not appear in anything that gets logged.
        Assert.That(ex.Message, Does.Not.Contain("SuperSecret123"));
        Assert.That(ex.ToString(), Does.Not.Contain("SuperSecret123"));
        // ...but is retained on the property for deliberate inspection.
        Assert.That(ex.Response, Is.EqualTo(secretBody));
    }

    [Test]
    public void GetAsync_MissingUserAgent_Throws()
    {
        var client = Client(new StubHandler(Json(HttpStatusCode.OK, "[]")), userAgent: "");

        Assert.ThrowsAsync<TownSuiteException>(async () =>
            await client.GetAsync("http://localhost/api", "token", CancellationToken.None));
    }

    [Test]
    public async Task GetAsync_SetsUserAgentAndBearerToken()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK, "[]"));
        var client = Client(handler, userAgent: "my-agent");

        await client.GetAsync("http://localhost/api", "my-token", CancellationToken.None);

        Assert.That(handler.LastRequest!.Headers.UserAgent.ToString(), Does.Contain("my-agent"));
        Assert.That(handler.LastRequest!.Headers.Authorization!.Scheme, Is.EqualTo("Bearer"));
        Assert.That(handler.LastRequest!.Headers.Authorization!.Parameter, Is.EqualTo("my-token"));
    }

    [Test]
    public async Task GetAsync_NoBearerToken_OmitsAuthorizationHeader()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK, "[]"));
        var client = Client(handler, userAgent: "my-agent");

        await client.GetAsync("http://localhost/api", "", CancellationToken.None);

        Assert.That(handler.LastRequest!.Headers.Authorization, Is.Null);
    }

    [Test]
    public void GetAsync_CancelledToken_Throws()
    {
        var client = Client(new StubHandler(Json(HttpStatusCode.OK, "[]")));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await client.GetAsync("http://localhost/api", "token", cts.Token));
    }
}
