using Coderunner.DistributedOutbox.Linq2Db;
using Coderunner.Presentation.Models;
using LinqToDB;

namespace Coderunner.Presentation;

public class CoderunnerDbContext : OutboxDbContext
{
    public CoderunnerDbContext(DataOptions options) :
        base(options)
    {
        (this as IDataContext).CloseAfterUse = true;

        Runs = this.GetTable<CodeRun>();
    }

    public ITable<CodeRun> Runs { get; }
}