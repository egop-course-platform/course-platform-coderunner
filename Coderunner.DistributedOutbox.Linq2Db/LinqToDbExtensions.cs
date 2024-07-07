using LinqToDB.Mapping;

namespace Coderunner.DistributedOutbox.Linq2Db;

public static class LinqToDbExtensions
{
    public static void AddOutboxMapping(this FluentMappingBuilder builder)
    {
        builder.Entity<OutboxDbEntry>()
            .HasIdentity(x => x.Id)
            .HasPrimaryKey(x => x.Id)
            .HasSchemaName("public")
            .HasTableName("outbox_events")
            .Property(x => x.Id)
            .HasColumnName("id")
            .Property(x => x.Type)
            .HasColumnName("type")
            .Property(x => x.Key)
            .HasColumnName("key")
            .Property(x => x.Date)
            .HasColumnName("date")
            .Property(x => x.Payload)
            .HasColumnName("payload")
            .Property(x => x.Status)
            .HasColumnName("status")
            .Property(x => x.Target)
            .HasColumnName("target")
            .HasDbType("text[]");
    }
}