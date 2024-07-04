using Coderunner.Presentation.Models;
using Coderunner.Presentation.Outbox;
using LinqToDB;

namespace Coderunner.Presentation;

public class CoderunnerDbContext : DataContext
{
    public CoderunnerDbContext(DataOptions options) :
        base(options)
    {
        (this as IDataContext).CloseAfterUse = true;

        Runs = this.GetTable<CodeRun>();
        OutboxEvents = this.GetTable<OutboxDbEntry>();
    }

    public ITable<CodeRun> Runs { get; }

    public ITable<OutboxDbEntry> OutboxEvents { get; }
}