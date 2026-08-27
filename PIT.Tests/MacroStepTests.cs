using PIT.Core.Automation;

namespace PIT.Tests;

public sealed class MacroStepTests
{
    [Theory]
    [InlineData(ActionKind.Delay, "Delay", "⏱")]
    [InlineData(ActionKind.KeyPress, "Key Press", "⌨")]
    [InlineData(ActionKind.MouseClick, "Mouse Click", "🖱")]
    [InlineData(ActionKind.MoveMouse, "Move Mouse", "↔")]
    [InlineData(ActionKind.RunMacro, "Run Macro", "▶")]
    public void Action_metadata_matches_the_step_kind(ActionKind kind, string label, string icon)
    {
        var step = new MacroStep { Action = new ActionDefinition { Kind = kind } };

        Assert.Equal(label, step.ActionLabel);
        Assert.Equal(icon, step.Icon);
    }

    [Fact]
    public void New_step_has_safe_execution_defaults()
    {
        var step = new MacroStep();

        Assert.True(step.IsEnabled);
        Assert.Equal(StepKind.Action, step.Kind);
        Assert.Equal(StepRunMode.Always, step.RunMode);
        Assert.NotEqual(Guid.Empty, step.Id);
    }
}
