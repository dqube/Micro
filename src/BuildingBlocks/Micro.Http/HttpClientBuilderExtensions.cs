using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Micro.Http;

public static class HttpClientBuilderExtensions
{
    public static IHttpClientBuilder AddCertificateAuthentication(
        this IHttpClientBuilder builder,
        string certificatePath,
        string password)
    {
        builder.ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            var certificateBytes = File.ReadAllBytes(certificatePath);
            var certificate = new X509Certificate2(certificateBytes, password);
            handler.ClientCertificates.Add(certificate);
            return handler;
        });
        return builder;
    }

    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount = 3)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == HttpStatusCode.NotFound)
            .WaitAndRetryAsync(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(
        int threshold = 5,
        TimeSpan? duration = null)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(threshold, duration ?? TimeSpan.FromSeconds(30));
    }

    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(TimeSpan? timeout = null)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(timeout ?? TimeSpan.FromSeconds(15));
    }
}
