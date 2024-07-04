using System.Text.Json;
using LinqToDB;

namespace Coderunner.Presentation.Outbox;

public interface IOutbox
{
    Task AddEventAsync(IOutboxEvent ev, CancellationToken cancellationToken);
}

public class Outbox : IOutbox
{
    private readonly CoderunnerDbContext _context;

    public Outbox(CoderunnerDbContext context)
    {
        _context = context;
    }

    public async Task AddEventAsync(IOutboxEvent ev, CancellationToken cancellationToken)
    {
        await _context.OutboxEvents.InsertAsync(
            () => new OutboxDbEntry()
            {
                Date = DateTime.UtcNow,
                Key = ev.EventKey,
                Payload = JsonSerializer.Serialize(ev.Payload, JsonSerializerOptions.Default),
                Status = "New",
                Target = "coderunner_outbox_events",
                Type = ev.EventType
            },
            token: cancellationToken
        );
    }
}