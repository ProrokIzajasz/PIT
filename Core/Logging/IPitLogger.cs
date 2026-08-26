namespace PIT.Core.Logging;

public interface IPitLogger
{
    event Action<ExecutionLogEntry>? EntryAdded;

    IReadOnlyList<ExecutionLogEntry> Entries { get; }

    void Info(string message);

    void Warning(string message);

    void Error(string message);
}