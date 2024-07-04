namespace Coderunner.Presentation.Models;

public class CodeRun
{
    public Guid Id { get; set; }

    public string Code { get; set; }

    public DateTime ScheduledAt { get; set; }
}