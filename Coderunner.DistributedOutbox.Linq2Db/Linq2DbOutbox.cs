using System.Text.Json;
using LinqToDB;

namespace Coderunner.DistributedOutbox.Linq2Db;

internal class Linq2DbOutbox : IOutbox
{
    private readonly OutboxDbContext _context;

    public Linq2DbOutbox(OutboxDbContext context)
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