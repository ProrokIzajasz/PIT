namespace PIT.Core.Input;

public interface IInputService
{
    Task PressKeyAsync(
        string key,
        string? modifiers,
        int holdMilliseconds,
        CancellationToken cancellationToken = default);

    Task KeyDownAsync(
        string key,
        string? modifiers,
        CancellationToken cancellationToken = default);

    Task KeyUpAsync(
        string key,
        string? modifiers,
        CancellationToken cancellationToken = default);

    Task MoveMouseAsync(
        int x,
        int y,
        MouseMoveMode mode,
        CancellationToken cancellationToken = default);

    Task ClickMouseAsync(
        MouseButton button,
        int? x,
        int? y,
        int downUpDelayMilliseconds,
        CancellationToken cancellationToken = default);

    Task MouseDownAsync(
        MouseButton button,
        int? x,
        int? y,
        CancellationToken cancellationToken = default);

    Task MouseUpAsync(
        MouseButton button,
        int? x,
        int? y,
        CancellationToken cancellationToken = default);
}