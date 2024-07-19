using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace Coderunner.Presentation.Services;

public record WebsocketItem(WebSocket Socket, CancellationTokenSource CancellationTokenSource);

public class WebsocketHolder
{
    private readonly ConcurrentDictionary<Guid, WebsocketItem> _webSockets = new();

    public WebsocketHolder()
    {
    }

    public bool Register(Guid codeRunId, WebSocket webSocket, CancellationTokenSource cancellationTokenSource)
    {
        cancellationTokenSource.Token.Register(() => _webSockets.TryRemove(codeRunId, out _));
        return _webSockets.TryAdd(codeRunId, new WebsocketItem(webSocket, cancellationTokenSource));
    }

    public bool Unregister(Guid codeRunId)
    {
        if (_webSockets.TryGetValue(codeRunId, out var websocketItem))
        {
            websocketItem.CancellationTokenSource.Cancel();
            _webSockets.TryRemove(codeRunId, out _);
            return true;
        }

        return false;
    }

    public async Task<bool> TryNotify(Guid codeRunId, object data, CancellationToken cancellationToken)
    {
        if (_webSockets.TryGetValue(codeRunId, out var websocketItem))
        {
            if (websocketItem.CancellationTokenSource.IsCancellationRequested)
            {
                return false;
            }

            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            try
            {
                using var ms = new MemoryStream(buffer);

                await JsonSerializer.SerializeAsync(ms, data, cancellationToken: cancellationToken);

                await websocketItem.Socket.SendAsync(
                    buffer.AsMemory()[..(int) ms.Length],
                    WebSocketMessageType.Text,
                    WebSocketMessageFlags.EndOfMessage,
                    cancellationToken
                );
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
            
            return true;
        }
        return false;
    }
}