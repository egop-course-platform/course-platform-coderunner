namespace Coderunner.DistributedOutbox;

public class OutboxDbEntry
{
    public long Id { get; set; }

    public string Type { get; set; } = "";

    public string Key { get; set; } = "";

    public DateTime Date { get; set; }

    public string Payload { get; set; } = "";

    public string Status { get; set; } = "";

    public string Target { get; set; } = "";
}