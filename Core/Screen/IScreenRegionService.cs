using PIT.Core.Ocr;

namespace PIT.Core.Screen;

public interface IScreenRegionService
{
    Task<ScreenRegionSnapshot> CaptureAsync(
        ScreenRegion region,
        CancellationToken cancellationToken = default);

    double CalculateDifferencePercent(
        ScreenRegionSnapshot baseline,
        ScreenRegionSnapshot current,
        int pixelTolerance);
}
