using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Roam.App.Services;

public interface IApiClient
{
    string BaseUrl { get; }
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool requireAuth = true, CancellationToken cancellationToken = default);
    Task<T?> GetAsync<T>(string endpoint, bool requireAuth = true, CancellationToken cancellationToken = default);
    Task<TResult?> PostAsync<TRequest, TResult>(string endpoint, TRequest data, bool requireAuth = true, CancellationToken cancellationToken = default);
    Task<bool> PostAsync<TRequest>(string endpoint, TRequest data, bool requireAuth = true, CancellationToken cancellationToken = default);
    Task<bool> PutAsync<TRequest>(string endpoint, TRequest data, bool requireAuth = true, CancellationToken cancellationToken = default);
}
