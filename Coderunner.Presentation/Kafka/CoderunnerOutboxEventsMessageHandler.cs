using LinqToDB;

namespace Coderunner.Presentation.Kafka;

public class CoderunnerOutboxEventsMessageHandler : IMessageHandler<CoderunnerOutboxEventsMessage>
{
    private readonly CoderunnerDbContext _context;
    private readonly IHostEnvironment _env;
    private readonly ILogger<CoderunnerOutboxEventsMessageHandler> _logger;

    public CoderunnerOutboxEventsMessageHandler(ILogger<CoderunnerOutboxEventsMessageHandler> logger, CoderunnerDbContext context, IHostEnvironment env)
    {
        _logger = logger;
        _context = context;
        _env = env;
    }

    public async Task Handle(CoderunnerOutboxEventsMessage message, CancellationToken cancellationToken)
    {
        var codeRun = await _context.Runs
            .FirstOrDefaultAsync(x => x.Id == message.CodeRunId, token: cancellationToken);

        if (codeRun is null)
        {
            _logger.LogWarning("Coderun {id} not found when running", message.CodeRunId);
            return;
        }

        var code = codeRun.Code;

        var path = _env.ContentRootPath;
        
        _logger.LogWarning("Running code {code} in {path}", code, path);
    }
}