using PIT.Core.Ocr;
using System.IO;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingSize = System.Drawing.Size;

namespace PIT.Infrastructure.Ocr;

public sealed class WindowsOcrService : IOcrService
{
    public async Task<OcrReadResult> ReadRegionAsync(
        ScreenRegion region,
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        if (!region.IsValid())
        {
            throw new InvalidOperationException("Region OCR ma niepoprawny rozmiar.");
        }

        if (region.Width > OcrEngine.MaxImageDimension || region.Height > OcrEngine.MaxImageDimension)
        {
            throw new InvalidOperationException(
                $"Region OCR jest za duży. Maksymalny wymiar OCR: {OcrEngine.MaxImageDimension}px.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var engine = CreateEngine(languageTag);

        using var softwareBitmap = await CaptureRegionAsSoftwareBitmapAsync(region);

        cancellationToken.ThrowIfCancellationRequested();

        var result = await engine.RecognizeAsync(softwareBitmap);

        return new OcrReadResult
        {
            Text = result.Text ?? "",
            Lines = result.Lines
                .Select(x => x.Text ?? "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList()
        };
    }

    private static OcrEngine CreateEngine(string languageTag)
    {
        if (!string.IsNullOrWhiteSpace(languageTag))
        {
            var language = new Language(languageTag);
            var engine = OcrEngine.TryCreateFromLanguage(language);

            if (engine is not null)
            {
                return engine;
            }
        }

        var fallback = OcrEngine.TryCreateFromUserProfileLanguages();

        if (fallback is null)
        {
            throw new InvalidOperationException("Nie udało się utworzyć silnika OCR Windows.");
        }

        return fallback;
    }

    private static async Task<SoftwareBitmap> CaptureRegionAsSoftwareBitmapAsync(ScreenRegion region)
    {
        using var bitmap = new DrawingBitmap(
            region.Width,
            region.Height,
            DrawingPixelFormat.Format32bppArgb);

        using (var graphics = DrawingGraphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                region.X,
                region.Y,
                0,
                0,
                new DrawingSize(region.Width, region.Height),
                System.Drawing.CopyPixelOperation.SourceCopy);
        }

        using var memoryStream = new MemoryStream();

        bitmap.Save(memoryStream, DrawingImageFormat.Png);

        var bytes = memoryStream.ToArray();

        using var randomAccessStream = new InMemoryRandomAccessStream();

        using (var outputStream = randomAccessStream.GetOutputStreamAt(0))
        {
            using var writer = new DataWriter(outputStream);

            writer.WriteBytes(bytes);

            await writer.StoreAsync();
            await writer.FlushAsync();

            writer.DetachStream();
        }

        randomAccessStream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);

        return await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);
    }
}