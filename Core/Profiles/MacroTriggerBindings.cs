namespace PIT.Core.Profiles;

public sealed class MacroTriggerBindings
{
    public TriggerTargetKind Mouse4TargetKind { get; set; } = TriggerTargetKind.Macro;

    public Guid? Mouse4MacroId { get; set; }

    public Guid? Mouse4SchemeId { get; set; }

    public TriggerRunMode Mouse4RunMode { get; set; } = TriggerRunMode.Once;

    public int Mouse4RepeatCount { get; set; } = 1;

    public bool BlockMouse4OriginalAction { get; set; }


    public TriggerTargetKind Mouse5TargetKind { get; set; } = TriggerTargetKind.Macro;

    public Guid? Mouse5MacroId { get; set; }

    public Guid? Mouse5SchemeId { get; set; }

    public TriggerRunMode Mouse5RunMode { get; set; } = TriggerRunMode.Once;

    public int Mouse5RepeatCount { get; set; } = 1;

    public bool BlockMouse5OriginalAction { get; set; }
}