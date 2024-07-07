namespace Coderunner.DistributedOutbox;

public interface IOutboxEventProducer
{
    Task<bool> Produce(string key, string value, CancellationToken cancellationToken);
}