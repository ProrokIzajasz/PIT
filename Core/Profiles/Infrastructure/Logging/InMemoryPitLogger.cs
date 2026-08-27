using PIT.Core.Logging;

namespace PIT.Infrastructure.Logging;

public sealed class InMemoryPitLogger : IPitLogger
{
    private readonly List<ExecutionLogEntry> _entries = new();

    public event Action<ExecutionLogEntry>? EntryAdded;

    public IReadOnlyList<ExecutionLogEntry> Entries => _entries;

    public void Info(string message)
    {
        Add("INFO", message);
    }

    public void Warning(string message)
    {
        Add("WARN", message);
    }

    public void Error(string message)
    {
        Add("ERROR", message);
    }

    private void Add(string level, string message)
    {
        var entry = new ExecutionLogEntry
        {
            Time = DateTime.Now,
            Level = level,
            Message = message
        };

        _entries.Add(entry);
        EntryAdded?.Invoke(entry);
    }
}