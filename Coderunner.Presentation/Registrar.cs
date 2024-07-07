using System.Diagnostics;
using LinqToDB;
using LinqToDB.AspNet;
using LinqToDB.AspNet.Logging;
using LinqToDB.DataProvider.PostgreSQL;

namespace Coderunner.Presentation;

public static class Registrar
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLinqToDBContext<CoderunnerDbContext>(
            (provider, options) =>
                options.UseConnectionString(
                        PostgreSQLTools.GetDataProvider(PostgreSQLVersion.v95),
                        configuration.GetConnectionString("Postgres") ??
                        throw new InvalidOperationException("Postgres connection string was not set")
                    )
                    .UseDefaultLogging(provider)
                    .UseTraceLevel(TraceLevel.Warning)
                    .UseMappingSchema(LinqToDbMappingSchema.Current)
        );

        return services;
    }

    public static IServiceCollection AddWarmup(this IServiceCollection services)
    {
        services.AddSingleton<IWarmup, Warmup>();

        return services;
    }
}