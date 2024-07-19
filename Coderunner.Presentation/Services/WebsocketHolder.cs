using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace Coderunner.Presentation.Services;

public record WebsocketItem(WebSocket Socket, CancellationTokenSource CancellationTokenSource);

public class WebsocketHolder
{
    private readonly ILogger<WebsocketHolder> _logger;
    private readonly ConcurrentDictionary<Guid, WebsocketItem> _webSockets = new();

    public WebsocketHolder(ILogger<WebsocketHolder> logger)
    {
        _logger = logger;
    }

    public bool Register(Guid codeRunId, WebSocket webSocket, CancellationTokenSource cancellationTokenSource)
    {
        _logger.LogInformation("Registering websocket for {coderun_id}", codeRunId);
        cancellationTokenSource.Token.Register(
            () =>
            {
                _logger.LogInformation("Executing cancellation of websocket for {coderun_id}", codeRunId);
                _webSockets.TryRemove(codeRunId, out _);
            }
        );

        var result = _webSockets.TryAdd(codeRunId, new WebsocketItem(webSocket, cancellationTokenSource));

        return result;
    }

    public bool Unregister(Guid codeRunId)
    {
        _logger.LogInformation("Unregistering websocket for {coderun_id}", codeRunId);
        if (_webSockets.TryGetValue(codeRunId, out var websocketItem))
        {
            websocketItem.CancellationTokenSource.Cancel();
            return true;
        }

        return false;
    }

    public async Task<bool> TryNotify(Guid codeRunId, object data, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Notifying websocket for {coderun_id}", codeRunId);
        if (_webSockets.TryGetValue(codeRunId, out var websocketItem))
        {
            if (websocketItem.CancellationTokenSource.IsCancellationRequested)
            {
                return false;
            }

            var buffer = JsonSerializer.SerializeToUtf8Bytes(data);

            await websocketItem.Socket.SendAsync(
                buffer,
                WebSocketMessageType.Text,
                WebSocketMessageFlags.EndOfMessage,
                cancellationToken
            );
            return true;
        }

        return false;
    }
}