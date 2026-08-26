using PIT.Core.Automation;

namespace PIT.Core.Execution.ActionHandlers;

public sealed class LogMessageActionHandler : IActionHandler
{
    public bool CanHandle(ActionKind kind)
    {
        return kind == ActionKind.LogMessage;
    }

    public Task ExecuteAsync(
        ActionDefinition action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var message = GetParameter(action, "Message", "Log message");
        context.Logger.Info(message);

        return Task.CompletedTask;
    }

    private static string GetParameter(ActionDefinition action, string key, string fallback)
    {
        return action.Parameters.TryGetValue(key, out var value)
            ? value
            : fallback;
    }
}