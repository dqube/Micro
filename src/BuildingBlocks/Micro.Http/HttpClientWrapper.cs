using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;

namespace Micro.Http;

public class HttpClientWrapper : IHttpClientWrapper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClientWrapperOptions _options;
    private readonly ILogger<HttpClientWrapper>? _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreakerPolicy;
    private readonly ICorrelationContext? _correlationContext;

    public HttpClientWrapper(
        IHttpClientFactory httpClientFactory,
        IOptions<HttpClientWrapperOptions> options,
        ILogger<HttpClientWrapper>? logger = null,
        ICorrelationContext? correlationContext = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? new HttpClientWrapperOptions();
        _logger = logger;
        _correlationContext = correlationContext;

        _retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => (int)msg.StatusCode >= 500)
            .WaitAndRetryAsync(
                _options.DefaultRetryCount,
                retryAttempt => _options.DefaultRetryDelay,
                onRetry: (outcome, delay, retryAttempt, context) =>
                {
                    _logger?.LogWarning(
                        "Retry {RetryAttempt} for {RequestUri} after {Delay}ms due to: {StatusCode}",
                        retryAttempt,
                        outcome.Result?.RequestMessage?.RequestUri,
                        delay.TotalMilliseconds,
                        outcome.Result?.StatusCode);
                });

        _circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                _options.CircuitBreakerThreshold,
                _options.CircuitBreakerDuration,
                onBreak: (outcome, breakDelay, context) =>
                {
                    _logger?.LogError(
                        "Circuit breaker opened for {Duration} due to: {StatusCode}",
                        breakDelay,
                        outcome.Result?.StatusCode);
                },
                onReset: (context) =>
                {
                    _logger?.LogInformation("Circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    _logger?.LogInformation("Circuit breaker half-open");
                });
    }

    public async Task<HttpResponse<T>> GetAsync<T>(
        string uri,
        Action<HttpRequestOptions>? configureOptions = null,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, uri), configureOptions, cancellationToken);
    }

    public async Task<HttpResponse<T>> PostAsync<T>(
        string uri,
        object content,
        Action<HttpRequestOptions>? configureOptions = null,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = CreateJsonContent(content)
        };
        return await SendAsync<T>(request, configureOptions, cancellationToken);
    }

    public async Task<HttpResponse<T>> PutAsync<T>(
        string uri,
        object content,
        Action<HttpRequestOptions>? configureOptions = null,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = CreateJsonContent(content)
        };
        return await SendAsync<T>(request, configureOptions, cancellationToken);
    }

    public async Task<HttpResponse<T>> PatchAsync<T>(
        string uri,
        object content,
        Action<HttpRequestOptions>? configureOptions = null,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri)
        {
            Content = CreateJsonContent(content)
        };
        return await SendAsync<T>(request, configureOptions, cancellationToken);
    }

    public async Task<HttpResponse<T>> DeleteAsync<T>(
        string uri,
        Action<HttpRequestOptions>? configureOptions = null,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<T>(new HttpRequestMessage(HttpMethod.Delete, uri), configureOptions, cancellationToken);
    }

    public async Task<HttpResponse<T>> SendAsync<T>(
        HttpRequestMessage request,
        Action<HttpRequestOptions>? configureOptions = null,
        CancellationToken cancellationToken = default)
    {
        var options = new HttpRequestOptions
        {
            ThrowOnError = _options.DefaultThrowOnError,
            CompletionOption = _options.DefaultCompletionOption
        };
        configureOptions?.Invoke(options);

        var clientName = options.ClientName ?? string.Empty;
        using var client = _httpClientFactory.CreateClient(clientName);

        if (options.ClientCertificate != null)
        {
            ConfigureCertificate(client, options);
        }

        if (options.Timeout.HasValue)
        {
            client.Timeout = options.Timeout.Value;
        }
        else if (_options.DefaultTimeout.HasValue)
        {
            client.Timeout = _options.DefaultTimeout.Value;
        }

        if (_options.IncludeCorrelationId && _correlationContext != null)
        {
            request.Headers.TryAddWithoutValidation(
                _options.CorrelationIdHeader,
                _correlationContext.CorrelationId);
        }

        if (options.Headers != null)
        {
            foreach (var header in options.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var stopwatch = Stopwatch.StartNew();
        LogRequest(request, options);

        try
        {
            var policy = Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);
            if (options.ResiliencePolicy != null)
            {
                policy = Policy.WrapAsync(options.ResiliencePolicy, policy);
            }

            var response = await policy.ExecuteAsync(async () =>
            {
                return await client.SendAsync(request, options.CompletionOption, cancellationToken);
            });

            stopwatch.Stop();
            var duration = stopwatch.Elapsed;

            LogResponse(response, duration, options);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (options.ThrowOnError)
                {
                    throw new HttpRequestException(
                        $"Request failed with status code {response.StatusCode}. Response: {responseContent}",
                        null,
                        response.StatusCode);
                }

                return new HttpResponse<T>
                {
                    StatusCode = response.StatusCode,
                    Headers = response.Headers,
                    Content = responseContent,
                    IsSuccessStatusCode = false,
                    Duration = duration
                };
            }

            try
            {
                var deserialized = string.IsNullOrEmpty(responseContent)
                    ? default
                    : JsonSerializer.Deserialize<T>(responseContent, _options.JsonSerializerOptions);

                return new HttpResponse<T>
                {
                    StatusCode = response.StatusCode,
                    Headers = response.Headers,
                    Content = responseContent,
                    Data = deserialized,
                    IsSuccessStatusCode = true,
                    Duration = duration
                };
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, "Failed to deserialize response from {Uri}", request.RequestUri);
                throw new HttpRequestException("Failed to deserialize response", ex);
            }
        }
        catch (BrokenCircuitException ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Circuit is open for {ClientName}", clientName);
            throw new HttpRequestException("Service unavailable due to circuit breaker state", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Request timeout for {Uri}", request.RequestUri);
            throw new HttpRequestException("Request timeout", ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Request failed for {Uri}", request.RequestUri);
            throw;
        }
    }

    private void ConfigureCertificate(HttpClient client, HttpRequestOptions options)
    {
        if (options.ClientCertificate is null)
            return;

        var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12
        };

        handler.ClientCertificates.Add(options.ClientCertificate);

        if (options.ServerCertificateCustomValidationCallback != null)
        {
            // Adapt the callback to accept nullable parameters as required by HttpClientHandler
            handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) =>
                options.ServerCertificateCustomValidationCallback.Invoke(
                    request,
                    cert!,
                    chain!,
                    errors
                );
        }

        var oldHandler = client.GetType()
            .GetField("_handler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .GetValue(client) as HttpMessageHandler;

        if (oldHandler is DelegatingHandler delegatingHandler)
        {
            delegatingHandler.InnerHandler = handler;
        }
    }

    private void LogRequest(HttpRequestMessage request, HttpRequestOptions options)
    {
        if (!_options.LogRequest) return;

        var message = $"Sending {request.Method} request to {request.RequestUri}";

        if (_options.LogRequestHeaders)
        {
            var headers = request.Headers
                .Select(h => $"{h.Key}: {string.Join(", ", h.Value)}");
            message += $"\nHeaders: {string.Join("; ", headers)}";
        }

        if (request.Content != null && options.LogRequestBody)
        {
            var body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            message += $"\nBody: {body}";
        }

        _logger?.LogInformation(message);
    }

    private void LogResponse(HttpResponseMessage response, TimeSpan duration, HttpRequestOptions options)
    {
        if (!_options.LogResponse) return;

        var uri = response.RequestMessage?.RequestUri;
        var message = $"Received {(int)response.StatusCode} {response.StatusCode} from {uri}";

        if (_options.LogResponseHeaders)
        {
            var headers = response.Headers
                .Select(h => $"{h.Key}: {string.Join(", ", h.Value)}");
            message += $"\nHeaders: {string.Join("; ", headers)}";
        }

        if (_options.LogRequestDuration)
        {
            message += $"\nDuration: {duration.TotalMilliseconds}ms";
        }

        _logger?.LogInformation(message);
    }

    private HttpContent? CreateJsonContent(object? content)
    {
        if (content == null) return null;

        var json = JsonSerializer.Serialize(content, _options.JsonSerializerOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}