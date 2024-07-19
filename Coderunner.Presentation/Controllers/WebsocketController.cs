using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Transactions;
using Coderunner.DistributedOutbox;
using Coderunner.Presentation.Events;
using Coderunner.Presentation.Models;
using Coderunner.Presentation.Services;
using LinqToDB;
using Microsoft.AspNetCore.Mvc;

namespace Coderunner.Presentation.Controllers;

public record RunCommand(string? Code);

public record CommandWrapper(string? Command);

public class WebsocketController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly WebsocketHolder _websocketHolder;
    private readonly ILogger<WebsocketController> _logger;

    public WebsocketController(WebsocketHolder websocketHolder, ILogger<WebsocketController> logger)
    {
        _websocketHolder = websocketHolder;
        _logger = logger;
    }

    [Route("/runner")]
    public async Task Get([FromServices] CoderunnerDbContext context, [FromServices] IOutbox outbox, CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        try
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            _logger.LogInformation("Websocket connected");

            var webSocketCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            WebSocketReceiveResult receiveResult;
            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            try
            {
                do
                {
                    receiveResult = await webSocket.ReceiveAsync(buffer, webSocketCancellation.Token);
                    if (receiveResult.CloseStatus.HasValue)
                    {
                        break;
                    }

                    var commandWrapper = JsonSerializer.Deserialize<CommandWrapper>(
                        buffer.AsSpan()[..receiveResult.Count],
                        JsonSerializerOptions
                    );

                    if (commandWrapper is null)
                    {
                        _logger.LogWarning("Null request in websocket");
                        continue;
                    }

                    switch (commandWrapper.Command)
                    {
                        case "run":
                        {
                            // if we have deserialized the command wrapper, then this deserialization won't return null
                            var runCodeRequest = JsonSerializer.Deserialize<RunCommand>(
                                buffer.AsSpan()[..receiveResult.Count],
                                JsonSerializerOptions
                            )!;

                            if (runCodeRequest.Code is null)
                            {
                                _logger.LogWarning("Run command has no code");
                                break;
                            }

                            var id = Guid.NewGuid();

                            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                            {
                                await context.Runs.InsertAsync(
                                    () => new CodeRun()
                                    {
                                        Id = id,
                                        Code = runCodeRequest.Code,
                                        ScheduledAt = DateTime.UtcNow
                                    },
                                    token: cancellationToken
                                );

                                await outbox.AddEventAsync(new RunCodeOutboxEvent(id), cancellationToken);
                                transaction.Complete();
                            }

                            _websocketHolder.Register(id, webSocket, webSocketCancellation);

                            break;
                        }
                        default:
                        {
                            _logger.LogWarning("Unknown command in websocket: {command}", commandWrapper.Command);
                            break;
                        }
                    }
                } while (!webSocketCancellation.IsCancellationRequested);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            await webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                receiveResult.CloseStatusDescription,
                CancellationToken.None
            );
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Websocket disconnected forcefully");
        }
    }
}