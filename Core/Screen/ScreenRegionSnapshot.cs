namespace PIT.Core.Screen;

public sealed class ScreenRegionSnapshot
{
    public int Width { get; set; }

    public int Height { get; set; }

    public byte[] BgrBytes { get; set; } = Array.Empty<byte>();
}
