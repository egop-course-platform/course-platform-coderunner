using Confluent.Kafka;

namespace Coderunner.DistributedOutbox.Kafka;

internal class OutboxEventProducer : IOutboxEventProducer
{
    private readonly IProducer<string, string> _producer;

    public OutboxEventProducer(IProducer<string, string> producer)
    {
        _producer = producer;
    }

    public async Task<bool> Produce(string key, string value, CancellationToken cancellationToken)
    {
        var result = await _producer.ProduceAsync(
            "coderunner_outbox_events",
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