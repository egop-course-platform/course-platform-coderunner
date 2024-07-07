using System.Transactions;
using Coderunner.DistributedOutbox;
using Coderunner.DistributedOutbox.Kafka;
using Coderunner.DistributedOutbox.Linq2Db;
using Coderunner.Presentation;
using Coderunner.Presentation.Dtos;
using Coderunner.Presentation.Events;
using Coderunner.Presentation.Kafka;
using Coderunner.Presentation.Models;
using LinqToDB;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host
    .UseSerilog(
        (builderContext, config) => { config.ReadFrom.Configuration(builderContext.Configuration); }
    );
builder.Services.AddWarmup();

builder.Services
    .AddDatabase(builder.Configuration)
    .WithLinq2DbOutbox<CoderunnerDbContext>()
    .WithKafkaProducer(
        builder.Configuration.GetConnectionString("Kafka")
        ?? throw new InvalidOperationException("Kafka connection string was not configured"),
        "coderunner"
    );

builder.Services.AddDefaultConsumer(builder.Configuration);

builder.Services
    .Consume<CoderunnerOutboxEventsMessage>()
    .WithTopicName("coderunner_outbox_events")
    .WithHandler<CoderunnerOutboxEventsMessageHandler>()
    .WithDeserializer<Utf8JsonDeserializer<CoderunnerOutboxEventsMessage>>()
    .Register();

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