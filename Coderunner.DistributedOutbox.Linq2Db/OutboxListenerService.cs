using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coderunner.DistributedOutbox.Linq2Db;

internal class OutboxListenerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxListenerService> _logger;

    private static readonly string[] InterestedEvents = ["New", "Failed"];

    public OutboxListenerService(IServiceProvider serviceProvider, ILogger<OutboxListenerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            {
                await using var scope = _serviceProvider.CreateAsyncScope();

                using var context = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<IOutboxEventProducer>();

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
                        _logger.LogInformation("Sent outbox event");
                    }
                    else
                    {
                        _logger.LogWarning("Failed sending outbox events to topic");
                    }
                }
            }

            await Task.Delay(2000, stoppingToken);
        }
    }
}