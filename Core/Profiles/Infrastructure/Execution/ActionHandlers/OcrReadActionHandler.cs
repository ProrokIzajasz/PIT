using PIT.Core.Automation;
using PIT.Core.Execution;
using PIT.Core.Ocr;

namespace PIT.Infrastructure.Execution.ActionHandlers;

public sealed class OcrReadActionHandler : IActionHandler
{
    private readonly IOcrService _ocrService;

    public OcrReadActionHandler(IOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    public bool CanHandle(ActionKind kind)
    {
        return kind == ActionKind.OcrRead;
    }

    public async Task ExecuteAsync(
        ActionDefinition action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var region = new ScreenRegion
        {
            X = GetIntParameter(action, "X", 0),
            Y = GetIntParameter(action, "Y", 0),
            Width = GetIntParameter(action, "Width", 300),
            Height = GetIntParameter(action, "Height", 120)
        };

        var language = GetParameter(action, "Language", "en-US");
        var contains = GetParameter(action, "Contains", "");

        var result = await _ocrService.ReadRegionAsync(region, language, cancellationToken);

        if (string.IsNullOrWhiteSpace(contains))
        {
            context.Logger.Info($"OCR: {result.Text}");
            return;
        }

        var found = result.Text.Contains(contains, StringComparison.OrdinalIgnoreCase);
        context.Logger.Info(found
            ? $"OCR found: {contains}"
            : $"OCR not found: {contains}");
    }

    private static string GetParameter(ActionDefinition action, string key, string fallback)
    {
        return action.Parameters.TryGetValue(key, out var value)
            ? value
            : fallback;
    }

    private static int GetIntParameter(ActionDefinition action, string key, int fallback)
    {
        return action.Parameters.TryGetValue(key, out var value)
               && int.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }
}
