using LinqToDB;

namespace Coderunner.Presentation.Outbox;

public class OutboxCleaner : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    private static readonly string[] InterestedEvents = ["Sent"];

    public OutboxCleaner(IServiceProvider serviceProvider)
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

                var cleanMoment = DateTime.UtcNow.AddDays(-7);

                await context.OutboxEvents
                    .Where(x => x.Date <= cleanMoment && InterestedEvents.Contains(x.Status))
                    .DeleteAsync(token: stoppingToken);
            }

            await Task.Delay(60_000, stoppingToken);
        }
    }
}