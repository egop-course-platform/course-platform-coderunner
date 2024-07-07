using Coderunner.Core;

namespace Coderunner.Presentation;

public class Warmup : IWarmup
{
    private readonly TaskCompletionSource _taskCompletionSource;
    private readonly ILogger<Warmup> _logger;

    public Warmup(IHostApplicationLifetime lifetime, ILogger<Warmup> logger)
    {
        _logger = logger;
        _taskCompletionSource = new TaskCompletionSource();
        lifetime.ApplicationStarted.Register(() => _taskCompletionSource.SetResult());
    }

    private Task? _wrapper;

    public Task WaitWarmup()
    {
        return _wrapper ??= _taskCompletionSource.Task.ContinueWith(
            _ =>
                _logger.LogInformation("Warmup complete")
        );
    }
}