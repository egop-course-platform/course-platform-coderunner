namespace Coderunner.DistributedOutbox;

public interface IOutboxEvent
{
    string EventKey { get; set; }

    string EventType { get; set; }

    object Payload { get; set; }
}