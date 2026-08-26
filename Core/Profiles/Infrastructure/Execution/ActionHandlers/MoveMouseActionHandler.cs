using PIT.Core.Automation;
using PIT.Core.Execution;
using PIT.Core.Input;

namespace PIT.Infrastructure.Execution.ActionHandlers;

public sealed class MoveMouseActionHandler : IActionHandler
{
    private readonly IInputService _inputService;

    public MoveMouseActionHandler(IInputService inputService)
    {
        _inputService = inputService;
    }

    public bool CanHandle(ActionKind kind)
    {
        return kind == ActionKind.MoveMouse;
    }

    public async Task ExecuteAsync(
        ActionDefinition action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var x = GetIntParameter(action, "X", 0);
        var y = GetIntParameter(action, "Y", 0);

        var modeText = GetParameter(action, "Mode", "Relative");

        if (!Enum.TryParse<MouseMoveMode>(modeText, ignoreCase: true, out var mode))
        {
            mode = MouseMoveMode.Relative;
        }

        context.Logger.Info($"MoveMouse: {mode}, X={x}, Y={y}");

        await _inputService.MoveMouseAsync(
            x,
            y,
            mode,
            cancellationToken);
    }

    private static string GetParameter(ActionDefinition action, string key, string fallback)
    {
        return action.Parameters.TryGetValue(key, out var value)
            ? value
            : fallback;
    }

    private static int GetIntParameter(ActionDefinition action, string key, int fallback)
    {
        return action.Parameters.TryGetValue(key, out var value)
               && int.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }
}