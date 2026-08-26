namespace PIT.Core.Logging;

public sealed class ExecutionLogEntry
{
    public DateTime Time { get; set; } = DateTime.Now;

    public string Level { get; set; } = "INFO";

    public string Message { get; set; } = "";

    public override string ToString()
    {
        return $"[{Time:HH:mm:ss}] {Level}: {Message}";
    }
}