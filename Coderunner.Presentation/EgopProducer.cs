using Confluent.Kafka;

namespace Coderunner.Presentation;

public interface IEgopProducer
{
    Task<bool> Produce(string topic, string key, string value, CancellationToken cancellationToken);
}

public class EgopProducer : IEgopProducer
{
    private readonly IProducer<string, string> _producer;

    public EgopProducer(IProducer<string,string> producer)
    {
        _producer = producer;
    }

    public async Task<bool> Produce(string topic, string key, string value, CancellationToken cancellationToken)
    {
        var result = await _producer.ProduceAsync(
            topic,
            new Message<string, string>()
            {
                Key = key,
                Value = value
            },
            cancellationToken
        );

        return result.Status == PersistenceStatus.Persisted;
    }
}