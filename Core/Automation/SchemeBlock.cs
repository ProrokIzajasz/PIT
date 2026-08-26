namespace PIT.Core.Automation;

public sealed class SchemeBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int Order { get; set; }

    public string Name { get; set; } = "New Block";

    public SchemeBlockKind Kind { get; set; } = SchemeBlockKind.RunMacro;

    public Guid? MacroId { get; set; }

    public Dictionary<string, string> Parameters { get; set; } = new();

    public string Icon
    {
        get
        {
            return Kind switch
            {
                SchemeBlockKind.RunMacro => "▶",
                SchemeBlockKind.Delay => "⏱",
                SchemeBlockKind.KeyPress => "⌨",
                SchemeBlockKind.KeyDown => "⌨↓",
                SchemeBlockKind.KeyUp => "⌨↑",
                SchemeBlockKind.Repeat => "⟳",
                SchemeBlockKind.EndRepeat => "END⟳",
                SchemeBlockKind.If => "IF",
                SchemeBlockKind.Else => "ELSE",
                SchemeBlockKind.EndIf => "END",
                _ => "•"
            };
        }
    }

    public string Label
    {
        get
        {
            if (Kind == SchemeBlockKind.If)
            {
                var condition = GetParameter("Condition", "");

                return condition switch
                {
                    "TimeElapsed" => "IF Time",
                    "OcrContains" => "IF OCR Contains",
                    "OcrSameInLast" => "IF OCR Same",
                    _ => "IF"
                };
            }

            return Kind switch
            {
                SchemeBlockKind.RunMacro => "Run Macro",
                SchemeBlockKind.Delay => "Delay",
                SchemeBlockKind.KeyPress => "KeyPress",
                SchemeBlockKind.KeyDown => "KeyDown",
                SchemeBlockKind.KeyUp => "KeyUp",
                SchemeBlockKind.Repeat => "Repeat",
                SchemeBlockKind.EndRepeat => "End Repeat",
                SchemeBlockKind.Else => "Else",
                SchemeBlockKind.EndIf => "End If",
                _ => Kind.ToString()
            };
        }
    }

    private string GetParameter(string key, string fallback)
    {
        return Parameters.TryGetValue(key, out var value)
            ? value
            : fallback;
    }
}
