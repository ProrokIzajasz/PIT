using System.Globalization;
using PIT.Core.Automation;
using PIT.Core.Logging;
using PIT.Core.Ocr;
using PIT.Core.Screen;

namespace PIT.Core.Execution;

public sealed class SchemeExecutor
{
    private readonly IPitLogger _logger;
    private readonly MacroExecutor _macroExecutor;
    private readonly IOcrService _ocrService;
    private readonly IScreenRegionService _screenRegionService;
    private readonly Random _random = new();

    public SchemeExecutor(
        IPitLogger logger,
        MacroExecutor macroExecutor,
        IOcrService ocrService,
        IScreenRegionService screenRegionService)
    {
        _logger = logger;
        _macroExecutor = macroExecutor;
        _ocrService = ocrService;
        _screenRegionService = screenRegionService;
    }

    public async Task ExecuteAsync(
        AutomationScheme scheme,
        IReadOnlyList<MacroDefinition> availableMacros,
        CancellationToken cancellationToken = default)
    {
        if (!scheme.IsEnabled)
        {
            return;
        }

        var heldKeys = new List<HeldKeyInfo>();

        try
        {
            await ExecuteInternalAsync(
                scheme,
                availableMacros,
                heldKeys,
                cancellationToken);
        }
        finally
        {
            await ReleaseHeldKeysAsync(heldKeys);
        }
    }

    private async Task ExecuteInternalAsync(
        AutomationScheme scheme,
        IReadOnlyList<MacroDefinition> availableMacros,
        List<HeldKeyInfo> heldKeys,
        CancellationToken cancellationToken)
    {
        var schemeStartedAtUtc = DateTime.UtcNow;
        var ifStack = new Stack<IfFrame>();
        var repeatStack = new Stack<RepeatFrame>();

        var blocks = scheme.Blocks
            .Where(x => x is not null)
            .OrderBy(x => x.Order)
            .ToList();

        var index = 0;

        while (index < blocks.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var block = blocks[index];

            switch (block.Kind)
            {
                case SchemeBlockKind.If:
                    await EnterIfBlockAsync(
                        block,
                        ifStack,
                        schemeStartedAtUtc,
                        cancellationToken);
                    index++;
                    continue;

                case SchemeBlockKind.Else:
                    EnterElseBlock(ifStack);
                    index++;
                    continue;

                case SchemeBlockKind.EndIf:
                    ExitIfBlock(ifStack);
                    index++;
                    continue;
            }

            if (!IsCurrentBranchActive(ifStack))
            {
                index++;
                continue;
            }

            switch (block.Kind)
            {
                case SchemeBlockKind.Repeat:
                    {
                        var count = GetIntParameter(block.Parameters, "Count", 1);
                        count = Math.Max(0, count);

                        if (count == 0)
                        {
                            index = FindMatchingEndRepeatIndex(blocks, index) + 1;
                            continue;
                        }

                        repeatStack.Push(new RepeatFrame
                        {
                            RepeatStartIndex = index,
                            RemainingIterations = count
                        });

                        index++;
                        break;
                    }

                case SchemeBlockKind.EndRepeat:
                    {
                        if (repeatStack.Count == 0)
                        {
                            index++;
                            break;
                        }

                        var frame = repeatStack.Peek();
                        frame.RemainingIterations--;

                        if (frame.RemainingIterations > 0)
                        {
                            index = frame.RepeatStartIndex + 1;
                            break;
                        }

                        repeatStack.Pop();
                        index++;
                        break;
                    }

                case SchemeBlockKind.RunMacro:
                    await ExecuteRunMacroBlockAsync(block, availableMacros, cancellationToken);
                    index++;
                    break;

                case SchemeBlockKind.Delay:
                    await ExecuteDelayBlockAsync(block, cancellationToken);
                    index++;
                    break;

                case SchemeBlockKind.KeyPress:
                    await ExecuteKeyboardBlockAsync(block, ActionKind.KeyPress, heldKeys, cancellationToken);
                    index++;
                    break;

                case SchemeBlockKind.KeyDown:
                    await ExecuteKeyboardBlockAsync(block, ActionKind.KeyDown, heldKeys, cancellationToken);
                    index++;
                    break;

                case SchemeBlockKind.KeyUp:
                    await ExecuteKeyboardBlockAsync(block, ActionKind.KeyUp, heldKeys, cancellationToken);
                    index++;
                    break;

                default:
                    index++;
                    break;
            }
        }
    }

    private static int FindMatchingEndRepeatIndex(
        IReadOnlyList<SchemeBlock> blocks,
        int repeatIndex)
    {
        var depth = 0;

        for (var i = repeatIndex + 1; i < blocks.Count; i++)
        {
            if (blocks[i].Kind == SchemeBlockKind.Repeat)
            {
                depth++;
                continue;
            }

            if (blocks[i].Kind != SchemeBlockKind.EndRepeat)
            {
                continue;
            }

            if (depth == 0)
            {
                return i;
            }

            depth--;
        }

        return repeatIndex;
    }

    private async Task EnterIfBlockAsync(
        SchemeBlock block,
        Stack<IfFrame> ifStack,
        DateTime schemeStartedAtUtc,
        CancellationToken cancellationToken)
    {
        var parentActive = IsCurrentBranchActive(ifStack);
        var conditionResult = false;

        if (parentActive)
        {
            conditionResult = await EvaluateConditionAsync(
                block,
                schemeStartedAtUtc,
                cancellationToken);
        }

        ifStack.Push(new IfFrame
        {
            ParentActive = parentActive,
            ConditionResult = conditionResult,
            InElseBranch = false
        });
    }

    private static void EnterElseBlock(Stack<IfFrame> ifStack)
    {
        if (ifStack.Count == 0)
        {
            return;
        }

        ifStack.Peek().InElseBranch = true;
    }

    private static void ExitIfBlock(Stack<IfFrame> ifStack)
    {
        if (ifStack.Count == 0)
        {
            return;
        }

        ifStack.Pop();
    }

    private async Task ExecuteRunMacroBlockAsync(
        SchemeBlock block,
        IReadOnlyList<MacroDefinition> availableMacros,
        CancellationToken cancellationToken)
    {
        if (block.MacroId is null)
        {
            _logger.Warning($"Blok '{block.Name}' nie ma przypisanego makra.");
            return;
        }

        var macro = availableMacros.FirstOrDefault(x => x.Id == block.MacroId.Value);

        if (macro is null)
        {
            _logger.Warning($"Nie znaleziono makra dla bloku '{block.Name}'.");
            return;
        }

        await _macroExecutor.ExecuteAsync(macro, cancellationToken);
    }

    private async Task ExecuteDelayBlockAsync(
        SchemeBlock block,
        CancellationToken cancellationToken)
    {
        var delayMs = ResolveDelay(block.Parameters);

        if (delayMs <= 0)
        {
            return;
        }

        await Task.Delay(delayMs, cancellationToken);
    }

    private async Task ExecuteKeyboardBlockAsync(
        SchemeBlock block,
        ActionKind actionKind,
        List<HeldKeyInfo> heldKeys,
        CancellationToken cancellationToken)
    {
        var key = GetParameter(block.Parameters, "Key", "Space");

        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warning($"Blok '{block.Name}' nie ma ustawionego klawisza.");
            return;
        }

        var action = new ActionDefinition
        {
            Kind = actionKind,
            Parameters =
            {
                ["Key"] = key
            }
        };

        if (actionKind == ActionKind.KeyPress)
        {
            action.Parameters["HoldMilliseconds"] = GetParameter(block.Parameters, "HoldMilliseconds", "40");
        }

        var modifiers = GetParameter(block.Parameters, "Modifiers", "");

        if (!string.IsNullOrWhiteSpace(modifiers))
        {
            action.Parameters["Modifiers"] = modifiers;
        }

        await ExecuteSingleActionAsTempMacroAsync(
            $"Scheme {actionKind} {key}",
            action,
            cancellationToken);

        if (actionKind == ActionKind.KeyDown)
        {
            heldKeys.Add(new HeldKeyInfo
            {
                Key = key,
                Modifiers = modifiers
            });
        }
        else if (actionKind == ActionKind.KeyUp)
        {
            var held = heldKeys.LastOrDefault(x =>
                string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Modifiers, modifiers, StringComparison.OrdinalIgnoreCase));

            if (held is not null)
            {
                heldKeys.Remove(held);
            }
        }
    }

    private async Task ReleaseHeldKeysAsync(List<HeldKeyInfo> heldKeys)
    {
        for (var i = heldKeys.Count - 1; i >= 0; i--)
        {
            var held = heldKeys[i];

            var action = new ActionDefinition
            {
                Kind = ActionKind.KeyUp,
                Parameters =
                {
                    ["Key"] = held.Key
                }
            };

            if (!string.IsNullOrWhiteSpace(held.Modifiers))
            {
                action.Parameters["Modifiers"] = held.Modifiers;
            }

            try
            {
                await ExecuteSingleActionAsTempMacroAsync(
                    $"Release held key {held.Key}",
                    action,
                    CancellationToken.None);
            }
            catch
            {
                // Awaryjne zwolnienie klawiszy nie powinno wywalać całego procesu.
            }
        }

        heldKeys.Clear();
    }

    private async Task ExecuteSingleActionAsTempMacroAsync(
        string macroName,
        ActionDefinition action,
        CancellationToken cancellationToken)
    {
        var tempMacro = new MacroDefinition
        {
            Name = macroName,
            Steps =
            {
                new MacroStep
                {
                    Order = 1,
                    Name = action.Kind.ToString(),
                    Kind = StepKind.Action,
                    Action = action
                }
            }
        };

        await _macroExecutor.ExecuteAsync(tempMacro, cancellationToken);
    }

    private async Task<bool> EvaluateConditionAsync(
        SchemeBlock block,
        DateTime schemeStartedAtUtc,
        CancellationToken cancellationToken)
    {
        var conditionText = GetParameter(
            block.Parameters,
            "Condition",
            SchemeConditionKind.TimeElapsed.ToString());

        if (!Enum.TryParse<SchemeConditionKind>(conditionText, ignoreCase: true, out var condition))
        {
            condition = SchemeConditionKind.TimeElapsed;
        }

        return condition switch
        {
            SchemeConditionKind.TimeElapsed => EvaluateTimeElapsed(block, schemeStartedAtUtc),
            SchemeConditionKind.OcrContains => await EvaluateOcrContainsAsync(block, cancellationToken),
            SchemeConditionKind.OcrSameInLast => await EvaluateOcrSameInLastAsync(block, cancellationToken),
            SchemeConditionKind.ScreenSameInLast => await EvaluateScreenSameInLastAsync(block, cancellationToken),
            _ => false
        };
    }

    private static bool EvaluateTimeElapsed(
        SchemeBlock block,
        DateTime schemeStartedAtUtc)
    {
        var requiredMs = GetIntParameter(block.Parameters, "Milliseconds", 0);
        var elapsedMs = (int)(DateTime.UtcNow - schemeStartedAtUtc).TotalMilliseconds;

        return elapsedMs >= requiredMs;
    }

    private async Task<bool> EvaluateOcrContainsAsync(
        SchemeBlock block,
        CancellationToken cancellationToken)
    {
        var region = BuildRegion(block.Parameters);
        var language = GetParameter(block.Parameters, "Language", "en-US");
        var contains = GetParameter(block.Parameters, "Contains", "");

        if (string.IsNullOrWhiteSpace(contains))
        {
            return false;
        }

        var result = await _ocrService.ReadRegionAsync(region, language, cancellationToken);

        var text = NormalizeOcrText(result.Text);
        var expected = NormalizeOcrText(contains);

        return text.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> EvaluateOcrSameInLastAsync(
        SchemeBlock block,
        CancellationToken cancellationToken)
    {
        var region = BuildRegion(block.Parameters);
        var language = GetParameter(block.Parameters, "Language", "en-US");
        var durationMs = GetIntParameter(block.Parameters, "DurationMilliseconds", 3000);
        var pollMs = GetIntParameter(block.Parameters, "PollMilliseconds", 500);

        durationMs = Math.Max(100, durationMs);
        pollMs = Math.Clamp(pollMs, 100, durationMs);

        var firstRead = await _ocrService.ReadRegionAsync(region, language, cancellationToken);
        var baseline = NormalizeOcrText(firstRead.Text);

        if (string.IsNullOrWhiteSpace(baseline))
        {
            return false;
        }

        var startedAtUtc = DateTime.UtcNow;

        while ((DateTime.UtcNow - startedAtUtc).TotalMilliseconds < durationMs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(pollMs, cancellationToken);

            var currentRead = await _ocrService.ReadRegionAsync(region, language, cancellationToken);
            var currentText = NormalizeOcrText(currentRead.Text);

            if (!string.Equals(baseline, currentText, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> EvaluateScreenSameInLastAsync(
        SchemeBlock block,
        CancellationToken cancellationToken)
    {
        var region = BuildRegion(block.Parameters);
        var durationMs = GetIntParameter(block.Parameters, "DurationMilliseconds", 3000);
        var pollMs = GetIntParameter(block.Parameters, "PollMilliseconds", 500);
        var pixelTolerance = GetIntParameter(block.Parameters, "PixelTolerance", 15);
        var maxDifferencePercent = GetDoubleParameter(block.Parameters, "MaxDifferencePercent", 0.50);

        durationMs = Math.Max(100, durationMs);
        pollMs = Math.Clamp(pollMs, 100, durationMs);
        maxDifferencePercent = Math.Clamp(maxDifferencePercent, 0.0, 100.0);

        var baseline = await _screenRegionService.CaptureAsync(region, cancellationToken);
        var startedAtUtc = DateTime.UtcNow;

        while ((DateTime.UtcNow - startedAtUtc).TotalMilliseconds < durationMs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(pollMs, cancellationToken);

            var current = await _screenRegionService.CaptureAsync(region, cancellationToken);
            var differencePercent = _screenRegionService.CalculateDifferencePercent(
                baseline,
                current,
                pixelTolerance);

            if (differencePercent > maxDifferencePercent)
            {
                return false;
            }
        }

        return true;
    }

    private static ScreenRegion BuildRegion(Dictionary<string, string> parameters)
    {
        return new ScreenRegion
        {
            X = GetIntParameter(parameters, "X", 0),
            Y = GetIntParameter(parameters, "Y", 0),
            Width = GetIntParameter(parameters, "Width", 300),
            Height = GetIntParameter(parameters, "Height", 120)
        };
    }

    private int ResolveDelay(Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("Milliseconds", out var fixedValue)
            && int.TryParse(fixedValue, out var fixedMs))
        {
            return Math.Max(0, fixedMs);
        }

        if (parameters.TryGetValue("MinMilliseconds", out var minValue)
            && parameters.TryGetValue("MaxMilliseconds", out var maxValue)
            && int.TryParse(minValue, out var minMs)
            && int.TryParse(maxValue, out var maxMs))
        {
            if (maxMs < minMs)
            {
                (minMs, maxMs) = (maxMs, minMs);
            }

            return _random.Next(minMs, maxMs + 1);
        }

        return 0;
    }

    private static bool IsCurrentBranchActive(IEnumerable<IfFrame> frames)
    {
        return frames.All(x => x.IsActive);
    }

    private static string GetParameter(
        Dictionary<string, string> parameters,
        string key,
        string fallback)
    {
        return parameters.TryGetValue(key, out var value)
            ? value
            : fallback;
    }

    private static int GetIntParameter(
        Dictionary<string, string> parameters,
        string key,
        int fallback)
    {
        return parameters.TryGetValue(key, out var value)
               && int.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private static double GetDoubleParameter(
        Dictionary<string, string> parameters,
        string key,
        double fallback)
    {
        if (!parameters.TryGetValue(key, out var value))
        {
            return fallback;
        }

        value = value.Replace(',', '.');

        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;
    }

    private static string NormalizeOcrText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return string.Join(
            " ",
            value
                .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()));
    }

    private sealed class IfFrame
    {
        public bool ParentActive { get; set; }

        public bool ConditionResult { get; set; }

        public bool InElseBranch { get; set; }

        public bool IsActive
        {
            get
            {
                if (!ParentActive)
                {
                    return false;
                }

                return InElseBranch
                    ? !ConditionResult
                    : ConditionResult;
            }
        }
    }

    private sealed class RepeatFrame
    {
        public int RepeatStartIndex { get; set; }

        public int RemainingIterations { get; set; }
    }

    private sealed class HeldKeyInfo
    {
        public string Key { get; set; } = "";

        public string Modifiers { get; set; } = "";
    }
}
