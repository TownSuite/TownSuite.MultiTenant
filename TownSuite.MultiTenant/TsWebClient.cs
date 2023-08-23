using System.Text.Json;
using System.Text.Json.Serialization;

namespace TownSuite.MultiTenant;

public class TsWebClient
{
    private System.Net.Http.HttpClient _httpClient;
    private readonly string _userAgent;

    public TsWebClient(System.Net.Http.HttpClient httpClient,
        string userAgent)
    {
        _httpClient = httpClient;
        _userAgent = userAgent;
    }
    
    void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_userAgent))
        {
            throw new TownSuiteException("User-Agent is required.");
        }
        
        request.Headers.Add("User-Agent", _userAgent);
    }

    public virtual async System.Threading.Tasks.Task<System.Collections.Generic.ICollection<WebSearchResponse>> GetAsync(
        string url, string bearerToken, System.Threading.CancellationToken cancellationToken)
    {
        
        var client_ = _httpClient;
        var disposeClient_ = false;
        try
        {
            using (var request_ = new System.Net.Http.HttpRequestMessage())
            {
                request_.Method = new System.Net.Http.HttpMethod("GET");
                request_.Headers.Accept.Add(
                    System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));
                
                request_.RequestUri = new System.Uri(url, System.UriKind.RelativeOrAbsolute);
                request_.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                PrepareRequest(client_, request_);

                var response_ = await client_
                    .SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                var disposeResponse_ = true;
                try
                {
                    var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
                    if (response_.Content != null && response_.Content.Headers != null)
                    {
                        foreach (var item_ in response_.Content.Headers)
                            headers_[item_.Key] = item_.Value;
                    }

                    var status_ = (int)response_.StatusCode;
                    if (status_ == 200)
                    {
                        var objectResponse_ =
                            await ReadObjectResponseAsync<System.Collections.Generic.ICollection<WebSearchResponse>>(
                                response_, headers_, cancellationToken).ConfigureAwait(false);
                        if (objectResponse_.Object == null)
                        {
                            throw new ApiException("Response was null which was not expected.", status_,
                                objectResponse_.Text, headers_, null);
                        }

                        return objectResponse_.Object;
                    }
                    else
                    {
                        var responseData_ = response_.Content == null
                            ? null
                            : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new ApiException(
                            "The HTTP status code of the response was not expected (" + status_ + ").", status_,
                            responseData_, headers_, null);
                    }
                }
                finally
                {
                    if (disposeResponse_)
                        response_.Dispose();
                }
            }
        }
        finally
        {
            if (disposeClient_)
                client_.Dispose();
        }
    }

    protected struct ObjectResponseResult<T>
    {
        public ObjectResponseResult(T responseObject, string responseText)
        {
            this.Object = responseObject;
            this.Text = responseText;
        }

        public T Object { get; }

        public string Text { get; }
    }

    protected async System.Threading.Tasks.Task<ObjectResponseResult<T>> ReadObjectResponseAsync<T>(
        System.Net.Http.HttpResponseMessage response,
        System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IEnumerable<string>> headers,
        System.Threading.CancellationToken cancellationToken)
    {
        if (response == null || response.Content == null)
        {
            return new ObjectResponseResult<T>(default(T), string.Empty);
        }
        
        try
        {
            using (var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            {
                var typedBody = JsonSerializer.Deserialize<T>(responseStream);
                return new ObjectResponseResult<T>(typedBody, string.Empty);
            }
        }
        catch (JsonException exception)
        {
            var message = "Could not deserialize the response body stream as " + typeof(T).FullName + ".";
            throw new ApiException(message, (int)response.StatusCode, string.Empty, headers, exception);
        }
    }
}

public class WebSearchResponse
{
    [JsonPropertyName("tenantId")] public string TenantId { get; set; }

    [JsonPropertyName("connections")]
    public System.Collections.Generic.ICollection<KeyValuePairOfStringAndString> Connections { get; set; }

    [JsonPropertyName("appSettings")]
    public System.Collections.Generic.ICollection<KeyValuePairOfStringAndString> AppSettings { get; set; }
}

public class KeyValuePairOfStringAndString
{
    [JsonPropertyName("key")] public string Key { get; set; }

    [JsonPropertyName("value")] public string Value { get; set; }
}

public class ApiException : System.Exception
{
    public int StatusCode { get; private set; }

    public string Response { get; private set; }

    public System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IEnumerable<string>> Headers { get; private set; }

    public ApiException(string message, int statusCode, string response, System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IEnumerable<string>> headers, System.Exception innerException)
        : base(message + "\n\nStatus: " + statusCode + "\nResponse: \n" + ((response == null) ? "(null)" : response.Substring(0, response.Length >= 512 ? 512 : response.Length)), innerException)
    {
        StatusCode = statusCode;
        Response = response; 
        Headers = headers;
    }

    public override string ToString()
    {
        return string.Format("HTTP Response: \n\n{0}\n\n{1}", Response, base.ToString());
    }
}

public class ApiException<TResult> : ApiException
{
    public TResult Result { get; private set; }

    public ApiException(string message, int statusCode, string response, System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IEnumerable<string>> headers, TResult result, System.Exception innerException)
        : base(message, statusCode, response, headers, innerException)
    {
        Result = result;
    }
}