using System.Text.Json;

namespace Micro.Caching;

// Serialization/JsonHybridCacheSerializer.cs
public class JsonHybridCacheSerializer : IHybridCacheSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonHybridCacheSerializer(JsonSerializerOptions options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            TypeInfoResolver = null,
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public byte[] Serialize<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, _options);
    }

    public T Deserialize<T>(byte[] bytes)
    {
        return JsonSerializer.Deserialize<T>(bytes, _options);
    }
}
