using Coderunner.Core;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coderunner.DistributedOutbox.Linq2Db;

internal class OutboxPoolerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWarmup _warmup;
    private readonly ILogger<OutboxPoolerService> _logger;

    private static readonly string[] InterestedEvents = ["New", "Failed"];

    public OutboxPoolerService(IServiceProvider serviceProvider, ILogger<OutboxPoolerService> logger, IWarmup warmup)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _warmup = warmup;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _warmup.WaitWarmup();

        _logger.LogInformation("Outbox pooler launched");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
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
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Outbox listener caught cancellation token");
            }
        }
    }
}