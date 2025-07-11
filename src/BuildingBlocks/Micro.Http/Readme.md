Registration and Usage
Service Registration
csharp
// In Program.cs or Startup.cs
builder.Services.AddHttpClientWrapper();

// Configure named client with all policies
builder.Services.AddHttpClient("SecureApiClient", client =>
{
    client.BaseAddress = new Uri("https://secure-api.example.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddCertificateAuthentication("path/to/certificate.pfx", "password")
.AddPolicyHandler(HttpClientBuilderExtensions.GetRetryPolicy())
.AddPolicyHandler(HttpClientBuilderExtensions.GetCircuitBreakerPolicy())
.AddPolicyHandler(HttpClientBuilderExtensions.GetTimeoutPolicy());
Configuration (appsettings.json)
json
{
  "HttpClientWrapper": {
    "DefaultThrowOnError": true,
    "DefaultTimeout": "00:00:30",
    "DefaultCompletionOption": "ResponseContentRead",
    "DefaultRetryCount": 3,
    "DefaultRetryDelay": "00:00:01",
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerDuration": "00:00:30",
    "LogRequest": true,
    "LogResponse": true,
    "LogRequestHeaders": true,
    "LogResponseHeaders": true,
    "LogRequestDuration": true,
    "CorrelationIdHeader": "X-Correlation-ID",
    "IncludeCorrelationId": true,
    "JsonSerializerOptions": {
      "PropertyNameCaseInsensitive": true,
      "PropertyNamingPolicy": "CamelCase",
      "WriteIndented": false
    }
  }
}
Example Usage
csharp
public class OrderService
{
    private readonly IHttpClientWrapper _httpClient;
    
    public OrderService(IHttpClientWrapper httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<Order> GetOrderAsync(string orderId)
    {
        var response = await _httpClient.GetAsync<Order>(
            $"/orders/{orderId}",
            options => options.ClientName = "SecureApiClient");
            
        return response.Data;
    }
    
    public async Task<PaymentResult> ProcessPayment(PaymentRequest payment)
    {
        var certificate = new X509Certificate2("path/to/client.pfx", "password");
        
        var response = await _httpClient.PostAsync<PaymentResult>(
            "/payments",
            payment,
            options => 
            {
                options.ClientName = "SecureApiClient";
                options.ClientCertificate = certificate;
                options.Timeout = TimeSpan.FromSeconds(45);
                options.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());
            });
            
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Payment failed: {response.Content}");
        }
        
        return response.Data;
    }
}
This complete implementation provides a production-ready HttpClient wrapper with all the requested features, proper dependency injection support, and comprehensive configuration options. The wrapper handles:

Certificate authentication

Resilience policies (retry, circuit breaker)

Correlation ID propagation

Detailed logging

Request/response tracking

Flexible configuration

Strong typing

Proper error handling