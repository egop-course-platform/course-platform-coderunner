using Microsoft.Extensions.DependencyInjection;

namespace Coderunner.DistributedOutbox.Linq2Db;

public static class Registrar
{
    public static IServiceCollection WithLinq2DbOutbox<TContext>(this IServiceCollection services)
        where TContext : OutboxDbContext
    {
        services.AddScoped<OutboxDbContext>(x => x.GetRequiredService<TContext>());
        services.AddScoped<IOutbox, Linq2DbOutbox>();
        services.AddHostedService<OutboxPoolerService>();
        services.AddHostedService<OutboxCleanerService>();

        return services;
    }
}