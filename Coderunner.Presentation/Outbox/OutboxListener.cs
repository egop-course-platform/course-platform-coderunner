using LinqToDB;

namespace Coderunner.Presentation.Outbox;

public class OutboxListener : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    private static readonly string[] InterestedEvents = ["New", "Failed"];

    public OutboxListener(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            {
                await using var scope = _serviceProvider.CreateAsyncScope();

                using var context = scope.ServiceProvider.GetRequiredService<CoderunnerDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<OutboxEventProducer>();

                var events = await context.OutboxEvents.Where(x => InterestedEvents.Contains(x.Status))
                    .OrderBy(x => x.Date)
                    .ThenBy(x => x.Id)
                    .ToListAsync(token: stoppingToken);

                foreach (var entry in events)
                {
                    var result = await producer.Produce(entry.Key, entry.Payload, stoppingToken);

                    if (result)
                    {
                        await context.OutboxEvents.Where(x => x.Id == entry.Id)
                            .Set(x => x.Status, "Sent")
                            .UpdateAsync(token: stoppingToken);
                    }
                }
            }

            await Task.Delay(2000, stoppingToken);
        }
    }
}