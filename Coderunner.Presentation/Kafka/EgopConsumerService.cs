using Coderunner.Core;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Coderunner.Presentation.Kafka;

public class EgopConsumerService<T> : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConsumerConfig _consumerConfig;
    private readonly IOptions<EgopConsumerServiceOptions<T>> _options;
    private readonly IWarmup _warmup;
    private readonly ILogger<EgopConsumerService<T>> _logger;

    public EgopConsumerService(IServiceProvider serviceProvider, ConsumerConfig consumerConfig, IOptions<EgopConsumerServiceOptions<T>> options, ILogger<EgopConsumerService<T>> logger, IWarmup warmup)
    {
        _serviceProvider = serviceProvider;
        _consumerConfig = consumerConfig;
        _options = options;
        _logger = logger;
        _warmup = warmup;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _warmup.WaitWarmup();

        using var consumer = new ConsumerBuilder<Ignore, byte[]>(_consumerConfig)
            .Build();
        
        _logger.LogInformation("Consumer launched for topic {topic}", _options.Value.Topic);

        consumer.Subscribe(_options.Value.Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = consumer.Consume(stoppingToken);

                var deserializer = _serviceProvider.GetRequiredService<IEgopDeserializer<T>>();

                var content = consumeResult.Message.Value;
                var message = deserializer.Deserialize(content);

                await using var serviceScope = _serviceProvider.CreateAsyncScope();

                var messageHandler = serviceScope.ServiceProvider.GetRequiredService<IMessageHandler<T>>();

                await messageHandler.Handle(message, stoppingToken);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(
                    "Consumer on {topic} caught cancellation token",
                    _options.Value.Topic
                );
            }
        }

        consumer.Close();
    }
}