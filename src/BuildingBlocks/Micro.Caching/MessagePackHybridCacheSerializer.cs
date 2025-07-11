using MessagePack;

namespace Micro.Caching;

// Serialization/MessagePackHybridCacheSerializer.cs
public class MessagePackHybridCacheSerializer : IHybridCacheSerializer
{
    private readonly MessagePackSerializerOptions _options;

    public MessagePackHybridCacheSerializer(MessagePackSerializerOptions options = null)
    {
        _options = options ?? MessagePackSerializerOptions.Standard
            .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
    }

    public byte[] Serialize<T>(T value)
    {
        return MessagePackSerializer.Serialize(value, _options);
    }

    public T Deserialize<T>(byte[] bytes)
    {
        return MessagePackSerializer.Deserialize<T>(bytes, _options);
    }
}