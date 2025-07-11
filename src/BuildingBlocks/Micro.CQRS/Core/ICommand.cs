
namespace Micro.CQRS.Core;

public interface ICommand<out TResponse> : IMessage { }
public interface ICommand : ICommand<Unit> { }
