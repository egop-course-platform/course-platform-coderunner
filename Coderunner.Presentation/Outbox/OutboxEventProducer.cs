using Confluent.Kafka;

namespace Coderunner.Presentation.Outbox;

public class OutboxEventProducer
{
    private readonly IEgopProducer _producer;

    public OutboxEventProducer(IEgopProducer producer)
    {
        _producer = producer;
    }

    public async Task<bool> Produce(string key, string value, CancellationToken cancellationToken)
    {
        return await _producer.Produce(
            "coderunner_outbox_events",
            key,
            value,
            cancellationToken
        );
    }
}