namespace PIT.Core.Automation;

public sealed class MacroStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "New Step";

    public int Order { get; set; }

    public bool IsEnabled { get; set; } = true;

    public StepKind Kind { get; set; } = StepKind.Action;

    public StepRunMode RunMode { get; set; } = StepRunMode.Always;

    public ConditionDefinition? Condition { get; set; }

    public ActionDefinition? Action { get; set; }

    public Guid? MacroReferenceId { get; set; }

    public string Icon
    {
        get
        {
            return Action?.Kind switch
            {
                ActionKind.Delay => "⏱",
                ActionKind.KeyPress => "⌨",
                ActionKind.KeyDown => "⌨↓",
                ActionKind.KeyUp => "⌨↑",
                ActionKind.MouseClick => "🖱",
                ActionKind.MouseDown => "🖱↓",
                ActionKind.MouseUp => "🖱↑",
                ActionKind.MoveMouse => "↔",
                ActionKind.OcrRead => "🔎",
                ActionKind.RunMacro => "▶",
                _ => "•"
            };
        }
    }

    public string ActionLabel
    {
        get
        {
            return Action?.Kind switch
            {
                ActionKind.Delay => "Delay",
                ActionKind.KeyPress => "Key Press",
                ActionKind.KeyDown => "Key Down",
                ActionKind.KeyUp => "Key Up",
                ActionKind.MouseClick => "Mouse Click",
                ActionKind.MouseDown => "Mouse Down",
                ActionKind.MouseUp => "Mouse Up",
                ActionKind.MoveMouse => "Move Mouse",
                ActionKind.OcrRead => "OCR Read",
                ActionKind.RunMacro => "Run Macro",
                _ => "-"
            };
        }
    }
}
