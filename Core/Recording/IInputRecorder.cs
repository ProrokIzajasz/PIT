namespace PIT.Core.Recording;

public interface IInputRecorder : IDisposable
{
    bool IsRecording { get; }

    event Action<RecordedInputEvent>? InputRecorded;

    event Action? StopRequested;

    void Start();

    void Stop();
}