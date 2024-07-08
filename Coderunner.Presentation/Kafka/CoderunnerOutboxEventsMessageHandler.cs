using System.Diagnostics;
using System.Reflection;
using Coderunner.Presentation.Models;
using Docker.DotNet;
using Docker.DotNet.Models;
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

        var path = Path.Combine(_env.ContentRootPath, "runs");

        _logger.LogWarning("Running code {code} in {path}", code, path);

        var runPath = Path.Combine(path, codeRun.Id.ToString("D"));
        try
        {
            Directory.CreateDirectory(runPath);
            Directory.CreateDirectory(Path.Combine(runPath, "src"));

            _logger.LogInformation("Created directory {path}", runPath);
            await CopyFiles(runPath, code, cancellationToken);

            var buildResult = await DockerBuild(codeRun, cancellationToken);

            if (buildResult.ErrorLines.Any(x => x.Contains("error")))
            {
                _logger.LogWarning("Build finished with error. aborting!");
                return;
            }

            if (buildResult.OutputLines.Any(x => x.Contains("success")))
            {
                _logger.LogWarning("Builder reported success. Launching app");
                var runResult = await DockerRun(codeRun, cancellationToken);
                
                _logger.LogInformation("Run result: Output: {@output}. Errors: {@errors}", runResult.OutputLines, runResult.ErrorLines);
            }
            else
            {
                _logger.LogWarning("Builder didn't report. Noop now");
                // TODO:
                return;
            }
        }
        finally
        {
            Directory.Delete(runPath, true);
        }
    }

    private async Task<(List<string> OutputLines, List<string> ErrorLines)> DockerRun(CodeRun codeRun, CancellationToken cancellationToken)
    {
        var process = new Process();

        var dockerRunArgs = $"run " +
                            $"-a stderr -a stdout " +
                            $"--name coderun-build-{codeRun.Id:D} " +
                            $"--rm " +
                            $"-m 100m --memory-swap 100m --cpus=\".5\" " +
                            $"-v /home/actions/course-platform/runs/{codeRun.Id:D}/artifacts:/app " +
                            $"-i " +
                            $"mcr.microsoft.com/dotnet/runtime:8.0 " +
                            $"sh -c \"dotnet /app/Runner.dll\"";

        var startInfo = new ProcessStartInfo("docker", dockerRunArgs)
        {
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
        return (outputLines, errorLines);
    }

    private async Task<(List<string> OutputLines, List<string> ErrorLines)> DockerBuild(CodeRun codeRun, CancellationToken cancellationToken)
    {
        var process = new Process();
        var dockerRunArgs = $"run " +
                            $"-a stderr -a stdout " +
                            $"--name coderun-build-{codeRun.Id:D} " +
                            $"--rm " +
                            $"-m 200m --memory-swap 200m --cpus=\".5\" " +
                            $"-v /home/actions/course-platform/runs/{codeRun.Id:D}/src:/src " +
                            $"-v /home/actions/course-platform/runs/{codeRun.Id:D}/artifacts:/app/publish " +
                            $"-i " +
                            $"mcr.microsoft.com/dotnet/sdk:8.0 " +
                            $"sh -c \"dotnet publish \\\"src/Runner.csproj\\\" -v quiet -c Release -o /app/publish && echo success\"";
        var startInfo = new ProcessStartInfo(
            "docker",
            dockerRunArgs
        )
        {
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

        await using var csProjFs = new FileStream(Path.Combine(runPath, "src", "Runner.csproj"), FileMode.Create);

        await csprojStream.CopyToAsync(csProjFs, cancellationToken);

        _logger.LogInformation("Created csproj");

        await using var programFs = new FileStream(Path.Combine(runPath, "src", "Program.cs"), FileMode.Create);

        await using var programStreamWriter = new StreamWriter(programFs);

        await programStreamWriter.WriteAsync(code);

        _logger.LogInformation("Created Program.cs");
    }
}