using System.Diagnostics;
using System.Reflection;
using Coderunner.Presentation.Models;
using Coderunner.Presentation.Services;
using Docker.DotNet;
using Docker.DotNet.Models;
using LinqToDB;

namespace Coderunner.Presentation.Kafka;

public class CoderunnerOutboxEventsMessageHandler : IMessageHandler<CoderunnerOutboxEventsMessage>
{
    private readonly CoderunnerDbContext _context;
    private readonly IHostEnvironment _env;
    private readonly WebsocketHolder _websocketHolder;
    private readonly ILogger<CoderunnerOutboxEventsMessageHandler> _logger;

    public CoderunnerOutboxEventsMessageHandler(ILogger<CoderunnerOutboxEventsMessageHandler> logger, CoderunnerDbContext context, IHostEnvironment env, WebsocketHolder websocketHolder)
    {
        _logger = logger;
        _context = context;
        _env = env;
        _websocketHolder = websocketHolder;
    }

    public async Task Handle(CoderunnerOutboxEventsMessage message, CancellationToken cancellationToken)
    {
        var codeRun = await _context.Runs
            .FirstOrDefaultAsync(x => x.Id == message.CodeRunId, token: cancellationToken);

        if (codeRun is null)
        {
            _logger.LogWarning("CoderunnerMessageHandler: Coderun {run_id} not found when running", message.CodeRunId);
            return;
        }

        _logger.LogInformation("CoderunnerMessageHandler: Started run {run_id}", message.CodeRunId);

        var path = Path.Combine(_env.ContentRootPath, "runs");

        await PerformRun(path, codeRun, cancellationToken);
    }

    private async Task PerformRun(string path, CodeRun codeRun, CancellationToken cancellationToken)
    {
        var codeRunId = codeRun.Id;
        var code = codeRun.Code;
        
        await _websocketHolder.TryNotify(
            codeRunId,
            new
            {
                Action = "build",
                Result = "launched"
            },
            cancellationToken
        );

        _logger.LogWarning("PerformRun: Running code {code} in {path}", code, path);

        var runPath = Path.Combine(path, codeRunId.ToString("D"));
        try
        {
            Directory.CreateDirectory(runPath);

            var srcPath = Path.Combine(runPath, "src");

            Directory.CreateDirectory(srcPath);

            _logger.LogInformation("PerformRun: Created directory {run_id}", codeRunId);

            await CopySrcFiles(srcPath, code, cancellationToken);

            var buildResult = await DockerBuild(codeRunId, cancellationToken);

            if (buildResult.ErrorLines.Any(x => x.Contains("error")))
            {
                _logger.LogWarning("PerformRun: Build finished with error. aborting!");
                await _websocketHolder.TryNotify(
                    codeRunId,
                    new
                    {
                        Action = "build",
                        Result = "aborted",
                        ErrorLines = buildResult.ErrorLines
                    },
                    cancellationToken
                );
                return;
            }

            if (buildResult.OutputLines.Any(x => x.Contains("success")))
            {
                _logger.LogWarning("PerformRun: Builder reported success. Launching app");
                await _websocketHolder.TryNotify(
                    codeRunId,
                    new
                    {
                        Action = "build",
                        Result = "succeeded"
                    },
                    cancellationToken
                );

                var runResult = await DockerRun(codeRunId, cancellationToken);

                _logger.LogInformation("PerformRun: run result: Output: {@output}. Errors: {@errors}", runResult.OutputLines, runResult.ErrorLines);

                await _websocketHolder.TryNotify(
                    codeRunId,
                    new
                    {
                        Action = "run",
                        Result = "succeeded",
                        OutputLines = runResult.OutputLines
                    },
                    cancellationToken
                );

                _websocketHolder.Unregister(codeRunId);
            }
            else
            {
                _logger.LogWarning("PerformRun: Builder didn't report. {output_lines}", buildResult.OutputLines);
                // this is actually a compilation error
                await _websocketHolder.TryNotify(
                    codeRunId,
                    new
                    {
                        Action = "build",
                        Result = "failed",
                        ErrorLines = buildResult.OutputLines
                    },
                    cancellationToken
                );
                _websocketHolder.Unregister(codeRunId);
            }
        }
        finally
        {
            Directory.Delete(runPath, true);
            _logger.LogInformation("PerformRun: Dropped run folder: {run_id}", codeRunId);
        }
    }

    private async Task<(List<string> OutputLines, List<string> ErrorLines)> DockerRun(Guid codeRunId, CancellationToken cancellationToken)
    {
        var dockerRunArgs = $"run " +
                            $"-a stderr -a stdout " +
                            $"--name coderun-run-{codeRunId:D} " +
                            $"--rm " +
                            $"-m 100m --memory-swap 100m --cpus=\".1\" " +
                            $"-v /home/actions/course-platform/runs/{codeRunId:D}/artifacts:/app " +
                            $"-i " +
                            $"-q " +
                            $"mcr.microsoft.com/dotnet/runtime:8.0 " +
                            $"sh -c \"dotnet /app/Runner.dll\"";

        return await ExecCli("docker", dockerRunArgs, cancellationToken);
    }

    private async Task<(List<string> OutputLines, List<string> ErrorLines)> DockerBuild(Guid codeRunId, CancellationToken cancellationToken)
    {
        var dockerRunArgs = $"run " +
                            $"-a stderr -a stdout " +
                            $"--name coderun-build-{codeRunId:D} " +
                            $"--rm " +
                            $"-m 200m --memory-swap 200m --cpus=\"1\" " +
                            $"-v /home/actions/course-platform/runs/{codeRunId:D}/src:/src " +
                            $"-v /home/actions/course-platform/runs/{codeRunId:D}/artifacts:/app/publish " +
                            $"-i " +
                            $"-q " +
                            $"mcr.microsoft.com/dotnet/sdk:8.0 " +
                            $"sh -c \"dotnet publish \\\"src/Runner.csproj\\\" -v quiet -c Release -o /app/publish && echo success\"";

        return await ExecCli("docker", dockerRunArgs, cancellationToken);
    }

    private async Task<(List<string> OutputLines, List<string> ErrorLines)> ExecCli(string program, string programArgs, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running CLI: {program} {args}", program, programArgs);

        var process = new Process();
        var startInfo = new ProcessStartInfo(
            program,
            programArgs
        )
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        process.StartInfo = startInfo;
        List<string> outputLines = [];
        List<string> errorLines = [];

        process.OutputDataReceived += OnProcessOnOutputDataReceived;
        process.ErrorDataReceived += OnProcessOnErrorDataReceived;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        process.OutputDataReceived -= OnProcessOnOutputDataReceived;
        process.ErrorDataReceived -= OnProcessOnErrorDataReceived;
        _logger.LogWarning("Build finished. Output: {output}. Error: {error}", string.Join("\n", outputLines), string.Join("\n", errorLines));

        return (outputLines, errorLines);

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
    }

    private async Task CopySrcFiles(string path, string code, CancellationToken cancellationToken)
    {
        using var csprojStream = typeof(CoderunnerOutboxEventsMessageHandler).Assembly
            .GetManifestResourceStream("Coderunner.Presentation.Runner.Runner.csproj")!;

        await using var csProjFs = new FileStream(Path.Combine(path, "Runner.csproj"), FileMode.Create);

        await csprojStream.CopyToAsync(csProjFs, cancellationToken);

        _logger.LogInformation("CopySrcFiles: Created csproj");

        await using var programFs = new FileStream(Path.Combine(path, "Program.cs"), FileMode.Create);

        await using var programStreamWriter = new StreamWriter(programFs);

        await programStreamWriter.WriteAsync(code);

        _logger.LogInformation("CopySrcFiles: Created Program.cs");
    }
}