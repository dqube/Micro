using System.Net;
using System.Net.Http.Headers;

namespace Micro.Http;

public class HttpResponse<T>
{
    public HttpStatusCode StatusCode { get; set; }
    public HttpResponseHeaders? Headers { get; set; }
    public string? Content { get; set; }
    public T? Data { get; set; }
    public bool IsSuccessStatusCode { get; set; }
    public TimeSpan Duration { get; set; }
}
