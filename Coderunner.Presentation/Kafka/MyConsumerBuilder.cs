namespace Coderunner.Presentation.Kafka;

public class MyConsumerBuilder<TMessage>
{
    private readonly IServiceCollection _services;
    private string _topic;
    private Type _handler;
    private Type _deserializer;

    public MyConsumerBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public MyConsumerBuilder<TMessage> WithTopicName(string topic)
    {
        _topic = topic;
        return this;
    }

    public MyConsumerBuilder<TMessage> WithHandler<THandler>()
        where THandler : IMessageHandler<TMessage>
    {
        _handler = typeof(THandler);
        return this;
    }

    public MyConsumerBuilder<TMessage> WithDeserializer<TDeserializer>()
        where TDeserializer : IEgopDeserializer<TMessage>
    {
        _deserializer = typeof(TDeserializer);

        return this;
    }

    public IServiceCollection Register()
    {
        _services
            .AddHostedService<EgopConsumerService<TMessage>>()
            .Configure<EgopConsumerServiceOptions<TMessage>>(x => x.Topic = _topic);

        _services.AddScoped(typeof(IMessageHandler<TMessage>), _handler);
        _services.AddSingleton(typeof(IEgopDeserializer<TMessage>), _deserializer);

        return _services;
    }
}