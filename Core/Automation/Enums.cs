namespace PIT.Core.Automation;

public enum ConditionKind
{
    TimeElapsed,
    ColorChanged,
    CoordinateChanged,
    OcrTextFound,
    ScreenImageFound
}

public enum ActionKind
{
    Delay,
    LogMessage,

    KeyPress,
    KeyDown,
    KeyUp,

    MouseClick,
    MouseDown,
    MouseUp,

    MoveMouse,

    OcrRead,

    RunMacro
}

public enum StepKind
{
    Action,
    Condition,
    MacroReference
}

public enum StepRunMode
{
    Always,
    WhenConditionTrue,
    WhenConditionFalse
}

public enum ExecutionState
{
    Idle,
    Running,
    Paused,
    Stopped,
    Completed,
    Failed
}
