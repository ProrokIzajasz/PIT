namespace PIT.Core.Automation;

public sealed class ActionDefinition
{
    public ActionKind Kind { get; set; }

    public Dictionary<string, string> Parameters { get; set; } = new();
}