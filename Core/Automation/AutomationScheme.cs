using System.Collections.ObjectModel;

namespace PIT.Core.Automation;

public sealed class AutomationScheme
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "New Scheme";

    public bool IsEnabled { get; set; } = true;

    public ObservableCollection<SchemeBlock> Blocks { get; set; } = new();

    public override string ToString()
    {
        return Name;
    }
}
