namespace Coderunner.Presentation.Outbox.Events;

public class RunCodeOutboxEvent : IOutboxEvent
{
    public record EventPayload(Guid CodeRunId);

    public string EventKey { get; set; }
    public string EventType { get; set; }
    public DateTime EventDate { get; set; }
    public object Payload { get; set; }

    public RunCodeOutboxEvent(Guid codeRunId)
    {
        EventKey = codeRunId.ToString("D");
        EventType = nameof(RunCodeOutboxEvent);
        EventDate = DateTime.UtcNow;
        Payload = new EventPayload(codeRunId);
    }
}