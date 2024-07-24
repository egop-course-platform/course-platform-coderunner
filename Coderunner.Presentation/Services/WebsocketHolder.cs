using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace Coderunner.Presentation.Services;

public record WebsocketItem(WebSocket Socket, TaskCompletionSource Tcs);

public class WebsocketHolder
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<WebsocketHolder> _logger;
    private readonly ConcurrentDictionary<Guid, WebsocketItem> _webSockets = new();

    public WebsocketHolder(ILogger<WebsocketHolder> logger)
    {
        _logger = logger;
    }

    public bool Register(Guid codeRunId, WebSocket webSocket, TaskCompletionSource tcs)
    {
        _logger.LogInformation("Registering websocket for {coderun_id}", codeRunId);

        var result = _webSockets.TryAdd(codeRunId, new WebsocketItem(webSocket, tcs));

        return result;
    }

    public bool Unregister(Guid codeRunId)
    {
        _logger.LogInformation("Unregistering websocket for {coderun_id}", codeRunId);
        if (_webSockets.TryRemove(codeRunId, out var websocketItem))
        {
            if (!websocketItem.Tcs.Task.IsCompleted)
            {
                _logger.LogInformation("Cancelling websocket by holder unregister");
                websocketItem.Tcs.SetResult();
            }

            return true;
        }

        return false;
    }

    public async Task<bool> TryNotify(Guid codeRunId, object data, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Notifying websocket for {coderun_id}", codeRunId);
        if (_webSockets.TryGetValue(codeRunId, out var websocketItem))
        {
            if (websocketItem.Tcs.Task.IsCompleted)
            {
                return false;
            }

            var buffer = JsonSerializer.SerializeToUtf8Bytes(data, JsonSerializerOptions);

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