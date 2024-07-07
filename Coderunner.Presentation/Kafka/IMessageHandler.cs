namespace Coderunner.Presentation.Kafka;

public interface IMessageHandler<in T>
{
    Task Handle(T message, CancellationToken cancellationToken);
}