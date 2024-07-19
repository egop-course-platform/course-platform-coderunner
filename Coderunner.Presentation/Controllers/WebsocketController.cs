using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Coderunner.Presentation.Controllers;

public class WebsocketController : ControllerBase
{
    private readonly ILogger<WebsocketController> _logger;

    public WebsocketController(ILogger<WebsocketController> logger)
    {
        _logger = logger;
    }

    [Route("/runner")]
    public async Task Get(CancellationToken cancellationToken)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            _logger.LogInformation("Websocket connected");

            var buffer = new byte[1024 * 4];
            var receiveResult = await webSocket.ReceiveAsync(buffer, cancellationToken);

            while (!receiveResult.CloseStatus.HasValue)
            {
                var request = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                
                _logger.LogInformation("Received websocket request: {request}", request);
            }

            await webSocket.CloseAsync(
                receiveResult.CloseStatus.Value,
                receiveResult.CloseStatusDescription,
                CancellationToken.None
            );
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}