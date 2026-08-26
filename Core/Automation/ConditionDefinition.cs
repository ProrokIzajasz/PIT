namespace PIT.Core.Automation;

public sealed class ConditionDefinition
{
    public ConditionKind Kind { get; set; }

    public Dictionary<string, string> Parameters { get; set; } = new();
}