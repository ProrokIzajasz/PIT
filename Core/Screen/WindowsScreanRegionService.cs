using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PIT.Core.Ocr;
using PIT.Core.Screen;

namespace PIT.Infrastructure.Screen;

public sealed class WindowsScreenRegionService : IScreenRegionService
{
    public Task<ScreenRegionSnapshot> CaptureAsync(
        ScreenRegion region,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!region.IsValid())
        {
            throw new InvalidOperationException("Region ekranu ma niepoprawny rozmiar.");
        }

        using var bitmap = new Bitmap(
            region.Width,
            region.Height,
            PixelFormat.Format24bppRgb);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                region.X,
                region.Y,
                0,
                0,
                new Size(region.Width, region.Height),
                CopyPixelOperation.SourceCopy);
        }

        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        try
        {
            var stride = Math.Abs(data.Stride);
            var rowBytes = bitmap.Width * 3;
            var output = new byte[rowBytes * bitmap.Height];
            var buffer = new byte[stride * bitmap.Height];

            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            for (var y = 0; y < bitmap.Height; y++)
            {
                Buffer.BlockCopy(
                    buffer,
                    y * stride,
                    output,
                    y * rowBytes,
                    rowBytes);
            }

            return Task.FromResult(new ScreenRegionSnapshot
            {
                Width = bitmap.Width,
                Height = bitmap.Height,
                BgrBytes = output
            });
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    public double CalculateDifferencePercent(
        ScreenRegionSnapshot baseline,
        ScreenRegionSnapshot current,
        int pixelTolerance)
    {
        if (baseline.Width != current.Width
            || baseline.Height != current.Height
            || baseline.BgrBytes.Length != current.BgrBytes.Length
            || baseline.BgrBytes.Length == 0)
        {
            return 100.0;
        }

        pixelTolerance = Math.Clamp(pixelTolerance, 0, 255);

        var changedPixels = 0;
        var pixelCount = baseline.Width * baseline.Height;

        for (var i = 0; i < baseline.BgrBytes.Length; i += 3)
        {
            var db = Math.Abs(baseline.BgrBytes[i] - current.BgrBytes[i]);
            var dg = Math.Abs(baseline.BgrBytes[i + 1] - current.BgrBytes[i + 1]);
            var dr = Math.Abs(baseline.BgrBytes[i + 2] - current.BgrBytes[i + 2]);

            if (db + dg + dr > pixelTolerance * 3)
            {
                changedPixels++;
            }
        }

        return changedPixels * 100.0 / pixelCount;
    }
}
