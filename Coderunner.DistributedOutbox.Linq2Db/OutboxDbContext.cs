using LinqToDB;

namespace Coderunner.DistributedOutbox.Linq2Db;

public class OutboxDbContext : DataContext
{
    public OutboxDbContext(DataOptions options) :
        base(options)
    {
        (this as IDataContext).CloseAfterUse = true;
        OutboxEvents = this.GetTable<OutboxDbEntry>();
    }

    public ITable<OutboxDbEntry> OutboxEvents { get; }
}