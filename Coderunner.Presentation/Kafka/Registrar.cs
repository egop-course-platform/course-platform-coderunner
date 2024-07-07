using Confluent.Kafka;

namespace Coderunner.Presentation.Kafka;

public static class Registrar
{
    public static MyConsumerBuilder<T> Consume<T>(this IServiceCollection services)
    {
        return new MyConsumerBuilder<T>(services);
    }

    public static IServiceCollection AddDefaultConsumer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(
            new ConsumerConfig()
            {
                BootstrapServers =
                    configuration.GetConnectionString("Kafka")
                    ?? throw new InvalidOperationException("Kafka connection string was not configured"),
                GroupId = "coderunner",
                AutoOffsetReset = AutoOffsetReset.Earliest
            }
        );

        return services;
    }
}