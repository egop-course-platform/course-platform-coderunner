using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;

namespace Coderunner.DistributedOutbox.Kafka;

public static class Registrar
{
    public static IServiceCollection WithKafkaProducer(
        this IServiceCollection services,
        string kafkaBootstrapServers,
        string clientId
    )
    {
        services.AddSingleton<IOutboxEventProducer, OutboxEventProducer>(
            x => new OutboxEventProducer(
                new ProducerBuilder<string, string>(
                    new ProducerConfig()
                    {
                        BootstrapServers = kafkaBootstrapServers,
                        ClientId = clientId,
                        LingerMs = 0,
                        MessageTimeoutMs = 3000
                    }
                ).Build()
            )
        );

        return services;
    }
}