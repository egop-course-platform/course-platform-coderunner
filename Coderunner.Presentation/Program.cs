using System.Diagnostics;
using System.Transactions;
using Coderunner.DistributedOutbox;
using Coderunner.DistributedOutbox.Kafka;
using Coderunner.DistributedOutbox.Linq2Db;
using Coderunner.Presentation;
using Coderunner.Presentation.Dtos;
using Coderunner.Presentation.Events;
using Coderunner.Presentation.Models;
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
).WithLinq2DbOutbox<CoderunnerDbContext>()
    .WithKafkaProducer(
    builder.Configuration.GetConnectionString("Kafka")
    ?? throw new InvalidOperationException("Kafka connection string was not configured"),
    "coderunner"
);

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