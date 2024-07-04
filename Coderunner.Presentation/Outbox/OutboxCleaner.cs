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
            await using var scope = _serviceProvider.CreateAsyncScope();

            await using var context = scope.ServiceProvider.GetRequiredService<CoderunnerDbContext>();

            var cleanMoment = DateTime.UtcNow.AddDays(-7);

            await context.OutboxEvents
                .Where(x => InterestedEvents.Contains(x.Status) && x.Date <= cleanMoment)
                .DeleteAsync(token: stoppingToken);

            await Task.Delay(60_000, stoppingToken);
        }
    }
}