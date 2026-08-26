using PIT.Core.Automation;

namespace PIT.Core.Execution.ActionHandlers;

public sealed class DelayActionHandler : IActionHandler
{
    private readonly Random _random = new();

    public bool CanHandle(ActionKind kind)
    {
        return kind == ActionKind.Delay;
    }

    public async Task ExecuteAsync(
        ActionDefinition action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var delayMs = ResolveDelay(action);

        context.Logger.Info($"Delay: {delayMs} ms");

        await Task.Delay(delayMs, cancellationToken);
    }

    private int ResolveDelay(ActionDefinition action)
    {
        if (action.Parameters.TryGetValue("Milliseconds", out var fixedValue)
            && int.TryParse(fixedValue, out var fixedMs))
        {
            return Math.Max(0, fixedMs);
        }

        if (action.Parameters.TryGetValue("MinMilliseconds", out var minValue)
            && action.Parameters.TryGetValue("MaxMilliseconds", out var maxValue)
            && int.TryParse(minValue, out var minMs)
            && int.TryParse(maxValue, out var maxMs))
        {
            if (maxMs < minMs)
            {
                (minMs, maxMs) = (maxMs, minMs);
            }

            return _random.Next(minMs, maxMs + 1);
        }

        return 1000;
    }
}