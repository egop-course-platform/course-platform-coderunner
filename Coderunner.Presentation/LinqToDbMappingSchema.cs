using Coderunner.Presentation.Models;
using Coderunner.Presentation.Outbox;
using LinqToDB.Mapping;

namespace Coderunner.Presentation;

public static class LinqToDbMappingSchema
{
    private static MappingSchema? _current;

    public static MappingSchema Current => _current ??= CreateSchema();

    private static MappingSchema CreateSchema()
    {
        var builder = new FluentMappingBuilder();

        builder.Entity<CodeRun>()
            .HasSchemaName("public")
            .HasTableName("coderuns")
            .HasIdentity(x => x.Id)
            .HasPrimaryKey(x => x.Id)
            .Property(x => x.Id)
            .HasColumnName("id")
            .Property(x => x.Code)
            .HasColumnName("code")
            .Property(x => x.ScheduledAt)
            .HasColumnName("scheduled_at");
        
        builder.AddOutboxMapping();

        builder.MappingSchema.SetConvertExpression<DateTime, DateTime>(
            dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        );

        var schema = builder.Build()
            .MappingSchema;

        return schema;
    }
}