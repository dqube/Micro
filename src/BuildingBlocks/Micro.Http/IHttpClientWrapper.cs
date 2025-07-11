using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Micro.Http;

public interface IHttpClientWrapper
{
    Task<HttpResponse<T>> GetAsync<T>(string uri, Action<HttpRequestOptions>? configureOptions = null, CancellationToken cancellationToken = default);
    Task<HttpResponse<T>> PostAsync<T>(string uri, object content, Action<HttpRequestOptions>? configureOptions = null, CancellationToken cancellationToken = default);
    Task<HttpResponse<T>> PutAsync<T>(string uri, object content, Action<HttpRequestOptions>? configureOptions = null, CancellationToken cancellationToken = default);
    Task<HttpResponse<T>> PatchAsync<T>(string uri, object content, Action<HttpRequestOptions>? configureOptions = null, CancellationToken cancellationToken = default);
    Task<HttpResponse<T>> DeleteAsync<T>(string uri, Action<HttpRequestOptions>? configureOptions = null, CancellationToken cancellationToken = default);
    Task<HttpResponse<T>> SendAsync<T>(HttpRequestMessage request, Action<HttpRequestOptions>? configureOptions = null, CancellationToken cancellationToken = default);
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHttpClientWrapper(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddOptions<HttpClientWrapperOptions>()
            .BindConfiguration(HttpClientWrapperOptions.SectionName);
        services.AddScoped<ICorrelationContext, CorrelationContext>();
        services.AddTransient<IHttpClientWrapper, HttpClientWrapper>();
        return services;
    }
}