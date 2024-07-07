namespace Coderunner.DistributedOutbox;

public interface IOutbox
{
    Task AddEventAsync(IOutboxEvent ev, CancellationToken cancellationToken);
}