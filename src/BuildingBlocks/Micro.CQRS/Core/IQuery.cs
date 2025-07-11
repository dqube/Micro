namespace Micro.CQRS.Core;

public interface IQuery<out TResponse> : IMessage { }
