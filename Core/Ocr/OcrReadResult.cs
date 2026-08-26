namespace PIT.Core.Ocr;

public sealed class OcrReadResult
{
    public string Text { get; set; } = "";

    public List<string> Lines { get; set; } = new();
}