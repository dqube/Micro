using System.Text.Json;

namespace Micro.Http;

public class HttpClientWrapperOptions
{
    public const string SectionName = "HttpClientWrapper";

    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public bool DefaultThrowOnError { get; set; } = true;
    public TimeSpan? DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public HttpCompletionOption DefaultCompletionOption { get; set; } = HttpCompletionOption.ResponseContentRead;

    public int DefaultRetryCount { get; set; } = 3;
    public TimeSpan DefaultRetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public int CircuitBreakerThreshold { get; set; } = 5;
    public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromSeconds(30);

    public bool LogRequest { get; set; } = true;
    public bool LogResponse { get; set; } = true;
    public bool LogRequestHeaders { get; set; } = true;
    public bool LogResponseHeaders { get; set; } = true;
    public bool LogRequestDuration { get; set; } = true;

    public string CorrelationIdHeader { get; set; } = "X-Correlation-ID";
    public bool IncludeCorrelationId { get; set; } = true;
}
