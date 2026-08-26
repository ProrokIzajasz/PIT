namespace PIT.Core.Ocr;

public sealed class ScreenRegion
{
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool IsValid()
    {
        return Width > 0 && Height > 0;
    }

    public override string ToString()
    {
        return $"X={X}, Y={Y}, W={Width}, H={Height}";
    }
}