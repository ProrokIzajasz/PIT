using PIT.Core.Automation;

namespace PIT.Core.Recording;

public sealed class RecordedInputEvent
{
    public ActionDefinition Action { get; set; } = new();

    public int DelayBeforeMilliseconds { get; set; }

    public string DisplayName { get; set; } = "";
}