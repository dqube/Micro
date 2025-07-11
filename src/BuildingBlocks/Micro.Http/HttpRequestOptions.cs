using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Polly;

namespace Micro.Http;

public class HttpRequestOptions
{
    public Dictionary<string, string> Headers { get; set; } = new();
    public HttpCompletionOption CompletionOption { get; set; }
    public bool ThrowOnError { get; set; }
    public bool LogRequestBody { get; set; } = true;
    public TimeSpan? Timeout { get; set; }
    public string? ClientName { get; set; }
    public IAsyncPolicy<HttpResponseMessage>? ResiliencePolicy { get; set; }
    public X509Certificate2? ClientCertificate { get; set; }
    public Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool>? ServerCertificateCustomValidationCallback { get; set; }
}
