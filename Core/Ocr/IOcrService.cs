namespace PIT.Core.Ocr;

public interface IOcrService
{
    Task<OcrReadResult> ReadRegionAsync(
        ScreenRegion region,
        string languageTag,
        CancellationToken cancellationToken = default);
}