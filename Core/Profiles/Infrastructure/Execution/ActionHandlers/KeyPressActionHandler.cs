using PIT.Core.Automation;
using PIT.Core.Execution;
using PIT.Core.Input;

namespace PIT.Infrastructure.Execution.ActionHandlers;

public sealed class KeyPressActionHandler : IActionHandler
{
    private readonly IInputService _inputService;

    public KeyPressActionHandler(IInputService inputService)
    {
        _inputService = inputService;
    }

    public bool CanHandle(ActionKind kind)
    {
        return kind is ActionKind.KeyPress or ActionKind.KeyDown or ActionKind.KeyUp;
    }

    public async Task ExecuteAsync(
        ActionDefinition action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var key = GetParameter(action, "Key", "");
        var modifiers = GetOptionalParameter(action, "Modifiers");
        var holdMs = GetIntParameter(action, "HoldMilliseconds", 40);

        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        switch (action.Kind)
        {
            case ActionKind.KeyPress:
                await _inputService.PressKeyAsync(key, modifiers, holdMs, cancellationToken);
                break;

            case ActionKind.KeyDown:
                await _inputService.KeyDownAsync(key, modifiers, cancellationToken);
                break;

            case ActionKind.KeyUp:
                await _inputService.KeyUpAsync(key, modifiers, cancellationToken);
                break;
        }
    }

    private static string GetParameter(ActionDefinition action, string key, string fallback)
    {
        return action.Parameters.TryGetValue(key, out var value)
            ? value
            : fallback;
    }

    private static string? GetOptionalParameter(ActionDefinition action, string key)
    {
        return action.Parameters.TryGetValue(key, out var value)
            ? value
            : null;
    }

    private static int GetIntParameter(ActionDefinition action, string key, int fallback)
    {
        return action.Parameters.TryGetValue(key, out var value)
               && int.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }
}