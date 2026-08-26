using PIT.Core.Automation;
using PIT.Core.Execution;
using PIT.Core.Input;

namespace PIT.Infrastructure.Execution.ActionHandlers;

public sealed class MouseClickActionHandler : IActionHandler
{
    private readonly IInputService _inputService;

    public MouseClickActionHandler(IInputService inputService)
    {
        _inputService = inputService;
    }

    public bool CanHandle(ActionKind kind)
    {
        return kind is ActionKind.MouseClick or ActionKind.MouseDown or ActionKind.MouseUp;
    }

    public async Task ExecuteAsync(
        ActionDefinition action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var buttonText = GetParameter(action, "Button", "Left");

        if (!Enum.TryParse<MouseButton>(buttonText, ignoreCase: true, out var button))
        {
            button = MouseButton.Left;
        }

        var x = GetNullableIntParameter(action, "X");
        var y = GetNullableIntParameter(action, "Y");
        var downUpDelayMs = GetIntParameter(action, "DownUpDelayMilliseconds", 40);

        switch (action.Kind)
        {
            case ActionKind.MouseClick:
                await _inputService.ClickMouseAsync(button, x, y, downUpDelayMs, cancellationToken);
                break;

            case ActionKind.MouseDown:
                await _inputService.MouseDownAsync(button, x, y, cancellationToken);
                break;

            case ActionKind.MouseUp:
                await _inputService.MouseUpAsync(button, x, y, cancellationToken);
                break;
        }
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

    private static int? GetNullableIntParameter(ActionDefinition action, string key)
    {
        return action.Parameters.TryGetValue(key, out var value)
               && int.TryParse(value, out var parsed)
            ? parsed
            : null;
    }
}