using System.Text.Json;

namespace Coderunner.Presentation.Kafka;

public interface IEgopDeserializer<T>
{
    T Deserialize(byte[] data);
}

public class Utf8JsonDeserializer<T> : IEgopDeserializer<T>
{
    public T Deserialize(byte[] data)
    {
        return JsonSerializer.Deserialize<T>(data)!;
    }
}