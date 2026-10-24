using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Roam.App.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string BaseUrl { get; }
    public Func<Task<string?>>? TokenProvider { get; set; }
    public Func<Task<bool>>? RefreshTokenHandler { get; set; }

    public ApiClient(HttpClient? httpClient = null, string? baseUrl = null)
    {
        BaseUrl = baseUrl ?? SignalRService.GetDefaultBaseUrl();
        _httpClient = httpClient ?? new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool requireAuth = true, CancellationToken cancellationToken = default)
    {
        if (requireAuth && TokenProvider != null)
        {
            var token = await TokenProvider();
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);

        // Attempt automatic token refresh on 401 Unauthorized
        if (response.StatusCode == HttpStatusCode.Unauthorized && requireAuth && RefreshTokenHandler != null)
        {
            var refreshed = await RefreshTokenHandler();
            if (refreshed && TokenProvider != null)
            {
                var newToken = await TokenProvider();
                if (!string.IsNullOrEmpty(newToken))
                {
                    // Clone request for retry
                    using var retryRequest = CloneHttpRequestMessage(request);
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                    response = await _httpClient.SendAsync(retryRequest, cancellationToken);
                }
            }
        }

        return response;
    }

    public async Task<T?> GetAsync<T>(string endpoint, bool requireAuth = true, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        var response = await SendAsync(request, requireAuth, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return default;

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    public async Task<TResult?> PostAsync<TRequest, TResult>(string endpoint, TRequest data, bool requireAuth = true, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await SendAsync(request, requireAuth, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return default;

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResult>(content, JsonOptions);
    }

    public async Task<bool> PostAsync<TRequest>(string endpoint, TRequest data, bool requireAuth = true, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await SendAsync(request, requireAuth, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PutAsync<TRequest>(string endpoint, TRequest data, bool requireAuth = true, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await SendAsync(request, requireAuth, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private static HttpRequestMessage CloneHttpRequestMessage(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri);
        if (req.Content != null)
        {
            var ms = new System.IO.MemoryStream();
            req.Content.CopyToAsync(ms).GetAwaiter().GetResult();
            ms.Position = 0;
            clone.Content = new StreamContent(ms);
            foreach (var h in req.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }
        foreach (var header in req.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
