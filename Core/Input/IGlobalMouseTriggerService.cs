namespace PIT.Core.Input;

public interface IGlobalMouseTriggerService : IDisposable
{
    event Action<GlobalMouseTriggerButton>? Triggered;

    bool IsRunning { get; }

    bool BlockMouse4 { get; set; }

    bool BlockMouse5 { get; set; }

    void Start();

    void Stop();
}