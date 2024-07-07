using LinqToDB;
using Microsoft.Extensions.DependencyInjection;

namespace Coderunner.DistributedOutbox.Linq2Db;

public static class Registrar
{
    public static IServiceCollection WithLinq2DbOutbox<TContext>(this IServiceCollection services)
        where TContext : DataContext
    {
        if (!typeof(TContext).IsAssignableTo(typeof(OutboxDbContext)))
        {
            throw new InvalidOperationException($"Linq2Db Context of type {typeof(TContext).Name} doesn't inherit {typeof(OutboxDbContext).Name}");
        }
        
        services.AddScoped<OutboxDbContext>(x => (x.GetRequiredService<TContext>() as OutboxDbContext)!);
        services.AddScoped<IOutbox, Linq2DbOutbox>();
        services.AddHostedService<OutboxListenerService>();
        services.AddHostedService<OutboxCleanerService>();

        return services;
    }
}