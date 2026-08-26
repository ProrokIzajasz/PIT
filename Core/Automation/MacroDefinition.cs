using System.Collections.ObjectModel;

namespace PIT.Core.Automation;

public sealed class MacroDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "New Macro";

    public bool IsEnabled { get; set; } = true;

    public ObservableCollection<MacroStep> Steps { get; set; } = new();

    public override string ToString()
    {
        return Name;
    }
}