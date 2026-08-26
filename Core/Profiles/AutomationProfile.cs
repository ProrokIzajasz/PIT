using PIT.Core.Automation;

namespace PIT.Core.Profiles;

public sealed class AutomationProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "New Profile";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public List<MacroDefinition> Macros { get; set; } = new();

    public List<AutomationScheme> Schemes { get; set; } = new();

    public MacroTriggerBindings TriggerBindings { get; set; } = new();

    public override string ToString()
    {
        return Name;
    }
}
