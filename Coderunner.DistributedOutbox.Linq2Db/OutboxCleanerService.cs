using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Coderunner.DistributedOutbox.Linq2Db;

public class OutboxCleanerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    private static readonly string[] InterestedEvents = ["Sent"];

    public OutboxCleanerService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
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
    }
}