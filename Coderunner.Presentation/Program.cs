using System.Diagnostics;
using System.Transactions;
using Coderunner.Presentation;
using Coderunner.Presentation.Dtos;
using Coderunner.Presentation.Models;
using Coderunner.Presentation.Outbox;
using Coderunner.Presentation.Outbox.Events;
using Confluent.Kafka;
using LinqToDB;
using LinqToDB.AspNet;
using LinqToDB.AspNet.Logging;
using LinqToDB.DataProvider.PostgreSQL;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IEgopProducer, EgopProducer>(
    x => new EgopProducer(
        new ProducerBuilder<string, string>(
            new ProducerConfig()
            {
                BootstrapServers = builder.Configuration.GetConnectionString("Kafka")
                                   ?? throw new InvalidOperationException("Kafka connection string was not configured"),
                ClientId = "coderunner"
            }
        ).Build()
    )
);

builder.Services.AddSingleton<OutboxEventProducer>();

builder.Services.AddLinqToDBContext<CoderunnerDbContext>(
    (provider, options) =>
        options.UseConnectionString(
                PostgreSQLTools.GetDataProvider(PostgreSQLVersion.v95),
                builder.Configuration.GetConnectionString("Postgres") ??
                throw new InvalidOperationException("Postgres connection string was not set")
            )
            .UseDefaultLogging(provider)
            .UseTraceLevel(TraceLevel.Warning)
            .UseMappingSchema(LinqToDbMappingSchema.Current)
);

builder.Services.AddHostedService<OutboxCleaner>();
builder.Services.AddHostedService<OutboxListener>();

builder.Services.AddScoped<IOutbox, Outbox>();

var app = builder.Build();

app.MapPost(
    "/schedule",
    async ([FromBody] ScheduleCodeRunRequest request, CoderunnerDbContext context, IOutbox outbox, CancellationToken cancellationToken) =>
    {
        var id = Guid.NewGuid();

        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            await context.Runs.InsertAsync(
                () => new CodeRun()
                {
                    Id = id,
                    Code = request.Code,
                    ScheduledAt = DateTime.UtcNow
                },
                token: cancellationToken
            );

            await outbox.AddEventAsync(new RunCodeOutboxEvent(id), cancellationToken);
            transaction.Complete();
        }

        // var process = new Process();
        // var startInfo = new ProcessStartInfo("docker image build");
        // process.StartInfo = startInfo;
        // process.Start();
        // await process.WaitForExitAsync();
    }
);

app.Run();