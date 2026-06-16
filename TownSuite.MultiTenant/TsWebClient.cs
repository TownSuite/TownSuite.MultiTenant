using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TownSuite.MultiTenant;

public class TsWebClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _clientName;
    private readonly string _userAgent;

    /// <summary>
    /// Creates a client backed by a caller-supplied <see cref="HttpClient"/>.
    /// Useful for tests, or when you manage the client's lifetime yourself.
    /// </summary>
    public TsWebClient(HttpClient httpClient, string userAgent)
    {
        _httpClient = httpClient;
        _userAgent = userAgent;
    }

    /// <summary>
    /// Preferred constructor: resolves a fresh <see cref="HttpClient"/> from the
    /// factory on each request so the underlying handler pool is managed
    /// (DNS refresh, no socket exhaustion) even though this object is a singleton.
    /// </summary>
    public TsWebClient(IHttpClientFactory httpClientFactory, string clientName, string userAgent)
    {
        _httpClientFactory = httpClientFactory;
        _clientName = clientName;
        _userAgent = userAgent;
    }

    public virtual async Task<ICollection<WebSearchResponse>> GetAsync(
        string url, string bearerToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userAgent))
        {
            throw new TownSuiteException("User-Agent is required.");
        }

        var client = _httpClientFactory?.CreateClient(_clientName) ?? _httpClient;

        using var request = new HttpRequestMessage(HttpMethod.Get,
            new Uri(url, UriKind.RelativeOrAbsolute));
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Add("User-Agent", _userAgent);

        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            var responseData = response.Content == null
                ? null
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new ApiException(
                $"The HTTP status code of the response was not expected ({(int)response.StatusCode}).",
                (int)response.StatusCode, responseData);
        }

        var result = await ReadObjectResponseAsync<ICollection<WebSearchResponse>>(response, cancellationToken)
            .ConfigureAwait(false);
        if (result == null)
        {
            throw new ApiException("Response was null which was not expected.", (int)response.StatusCode, null);
        }

        return result;
    }

    private static async Task<T> ReadObjectResponseAsync<T>(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content == null)
        {
            return default;
        }

        try
        {
            await using var responseStream = await response.Content
                .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(responseStream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            var message = "Could not deserialize the response body stream as " + typeof(T).FullName + ".";
            throw new ApiException(message, (int)response.StatusCode, string.Empty, exception);
        }
    }
}

public class WebSearchResponse
{
    [JsonPropertyName("tenantId")] public string TenantId { get; set; }

    [JsonPropertyName("connections")]
    public ICollection<KeyValuePairOfStringAndString> Connections { get; set; }

    [JsonPropertyName("appSettings")]
    public ICollection<KeyValuePairOfStringAndString> AppSettings { get; set; }
}

public class KeyValuePairOfStringAndString
{
    [JsonPropertyName("key")] public string Key { get; set; }

    [JsonPropertyName("value")] public string Value { get; set; }
}

public class ApiException : Exception
{
    public int StatusCode { get; }

    public string Response { get; }

    public ApiException(string message, int statusCode, string response, Exception innerException = null)
        : base(message + "\n\nStatus: " + statusCode + "\nResponse: \n" +
               (response == null ? "(null)" : response.Substring(0, Math.Min(response.Length, 512))), innerException)
    {
        StatusCode = statusCode;
        Response = response;
    }

    public override string ToString()
    {
        return $"HTTP Response: \n\n{Response}\n\n{base.ToString()}";
    }
}
