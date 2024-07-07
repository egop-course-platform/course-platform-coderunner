using System.Diagnostics;
using System.Reflection;
using Coderunner.Presentation.Models;
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

        var runPath = Path.Combine(path, codeRun.Id.ToString("D"));
        try
        {
            var dir = Directory.CreateDirectory(runPath);

            _logger.LogInformation("Created directory {path}", runPath);
            await CopyFiles(runPath, code, cancellationToken);

            var buildResult = await DockerBuild(codeRun, cancellationToken, runPath);

            if (buildResult.OutputLines.Any(x => x.Contains("error")))
            {
                _logger.LogWarning("Build finished with error. aborting!");
                return;
            }

            await DockerRun(codeRun, cancellationToken, runPath);
        }
        finally
        {
            Directory.Delete(runPath, true);
        }
    }

    private async Task DockerRun(CodeRun codeRun, CancellationToken cancellationToken, string runPath)
    {
        var process = new Process();

        var dockerRunArgs = $"run " +
                            $"-a stderr -a stdout " +
                            $"--name coderun-build-{codeRun.Id:D} " +
                            $"--rm " +
                            $"-m 150m --cpus=\".5\" " +
                            $"-v {runPath}/artifacts:/app " +
                            $"-i " +
                            $"mcr.microsoft.com/dotnet/runtime:8.0 " +
                            $"sh -c \"dotnet /app/Runner.dll -v quiet -c Release -o /app/publish\"";
        
        var startInfo = new ProcessStartInfo("docker", dockerRunArgs)
        {
            WorkingDirectory = runPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        process.StartInfo = startInfo;
        List<string> outputLines = [];
        List<string> errorLines = [];

        void OnProcessOnOutputDataReceived(object _, DataReceivedEventArgs args)
        {
            if (args.Data is not null)
            {
                outputLines.Add(args.Data);
            }
        }

        void OnProcessOnErrorDataReceived(object _, DataReceivedEventArgs args)
        {
            if (args.Data is not null)
            {
                errorLines.Add(args.Data);
            }
        }

        process.OutputDataReceived += OnProcessOnOutputDataReceived;
        process.ErrorDataReceived += OnProcessOnErrorDataReceived;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        // var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        process.OutputDataReceived -= OnProcessOnOutputDataReceived;
        process.ErrorDataReceived -= OnProcessOnErrorDataReceived;
        _logger.LogWarning("Run finished. Output: {output}. Error: {error}", string.Join("\n", outputLines), string.Join("\n", errorLines));
    }

    private async Task<(List<string> OutputLines, List<string> ErrorLines)> DockerBuild(CodeRun codeRun, CancellationToken cancellationToken, string runPath)
    {
        var process = new Process();
        var dockerRunArgs = $"run " +
                            $"-a stderr -a stdout " +
                            $"--name coderun-build-{codeRun.Id:D} " +
                            $"--rm " +
                            $"-m 150m --cpus=\".5\" " +
                            $"-v {runPath}:/src " +
                            $"-v {runPath}/artifacts:/app/publish " +
                            $"-i " +
                            $"mcr.microsoft.com/dotnet/sdk:8.0 " +
                            $"sh -c \"dotnet publish \\\"src/Runner.csproj\\\" -v quiet -c Release -o /app/publish && echo success\"";
        var startInfo = new ProcessStartInfo("docker", 
            dockerRunArgs)
        {
            WorkingDirectory = runPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        process.StartInfo = startInfo;
        List<string> outputLines = [];
        List<string> errorLines = [];

        void OnProcessOnOutputDataReceived(object _, DataReceivedEventArgs args)
        {
            if (args.Data is not null)
            {
                outputLines.Add(args.Data);
            }
        }

        void OnProcessOnErrorDataReceived(object _, DataReceivedEventArgs args)
        {
            if (args.Data is not null)
            {
                errorLines.Add(args.Data);
            }
        }

        process.OutputDataReceived += OnProcessOnOutputDataReceived;
        process.ErrorDataReceived += OnProcessOnErrorDataReceived;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        // var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        process.OutputDataReceived -= OnProcessOnOutputDataReceived;
        process.ErrorDataReceived -= OnProcessOnErrorDataReceived;
        _logger.LogWarning("Build finished. Output: {output}. Error: {error}", string.Join("\n", outputLines), string.Join("\n", errorLines));
        
        return (outputLines, errorLines);
    }

    private async Task CopyFiles(string runPath, string code, CancellationToken cancellationToken)
    {
        using var csprojStream = typeof(CoderunnerOutboxEventsMessageHandler).Assembly
            .GetManifestResourceStream("Coderunner.Presentation.Runner.Runner.csproj")!;

        await using var csProjFs = new FileStream(Path.Combine(runPath, "Runner.csproj"), FileMode.Create);

        await csprojStream.CopyToAsync(csProjFs, cancellationToken);

        _logger.LogInformation("Created csproj");

        await using var programFs = new FileStream(Path.Combine(runPath, "Program.cs"), FileMode.Create);

        await using var programStreamWriter = new StreamWriter(programFs);

        await programStreamWriter.WriteAsync(code);

        _logger.LogInformation("Created Program.cs");
    }
}