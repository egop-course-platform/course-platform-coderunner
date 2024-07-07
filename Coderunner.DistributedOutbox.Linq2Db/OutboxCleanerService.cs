using Coderunner.Core;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coderunner.DistributedOutbox.Linq2Db;

public class OutboxCleanerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWarmup _warmup;
    private readonly ILogger<OutboxCleanerService> _logger;

    private static readonly string[] InterestedEvents = ["Sent"];

    public OutboxCleanerService(IServiceProvider serviceProvider, ILogger<OutboxCleanerService> logger, IWarmup warmup)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _warmup = warmup;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _warmup.WaitWarmup();

        _logger.LogInformation("Outbox cleaner launched");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                {
                    await using var scope = _serviceProvider.CreateAsyncScope();

                    using var context = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();

                    var cleanMoment = DateTime.UtcNow.AddDays(-7);

                    await context.OutboxEvents
                        .Where(x => x.Date <= cleanMoment && InterestedEvents.Contains(x.Status))
                        .DeleteAsync(token: stoppingToken);
                }

                await Task.Delay(60_000, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Outbox cleaner caught cancellation token");
            }
        }
    }
}