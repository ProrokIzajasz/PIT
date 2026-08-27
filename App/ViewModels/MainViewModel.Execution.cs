using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using PIT.Core.Automation;
using PIT.Core.Execution;
using PIT.Core.Execution.ActionHandlers;
using PIT.Core.Input;
using PIT.Core.Logging;
using PIT.Core.Ocr;
using PIT.Core.Screen;
using PIT.Core.Profiles;
using PIT.Core.Recording;
using PIT.Infrastructure.Execution.ActionHandlers;
using PIT.Infrastructure.Input;
using PIT.Infrastructure.Logging;
using PIT.Infrastructure.Ocr;
using PIT.Infrastructure.Screen;
using PIT.Infrastructure.Profiles;
using PIT.Infrastructure.Recording;

namespace PIT.App.ViewModels;

public sealed partial class MainViewModel
{
    private async Task RunSelectedSchemeAsync()
    {
        if (SelectedProfile is null || SelectedScheme is null)
        {
            return;
        }

        if (_macroCancellation is not null)
        {
            return;
        }

        if (_inputRecorder.IsRecording)
        {
            return;
        }

        _macroCancellation = new CancellationTokenSource();

        try
        {
            await _schemeExecutor.ExecuteAsync(
                SelectedScheme,
                SelectedProfile.Macros,
                _macroCancellation.Token);
        }
        finally
        {
            _macroCancellation.Dispose();
            _macroCancellation = null;
        }
    }

    private void AddSchemeRunMacroBlock()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        var block = new SchemeBlock
        {
            Order = SelectedScheme.Blocks.Count + 1,
            Kind = SchemeBlockKind.RunMacro,
            Name = SelectedMacro is null
                ? "Run Macro"
                : $"Run {SelectedMacro.Name}",
            MacroId = SelectedMacro?.Id
        };

        SelectedScheme.Blocks.Add(block);
        SelectedSchemeBlock = block;
        ReorderSchemeBlocks();
    }

    private void AddSchemeDelayBlock()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        var block = new SchemeBlock
        {
            Order = SelectedScheme.Blocks.Count + 1,
            Kind = SchemeBlockKind.Delay,
            Name = "Delay 1000 ms",
            Parameters =
            {
                ["Milliseconds"] = "1000"
            }
        };

        SelectedScheme.Blocks.Add(block);
        SelectedSchemeBlock = block;
        ReorderSchemeBlocks();
    }

    private void AddSchemeKeyPressBlock()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        var block = new SchemeBlock
        {
            Order = SelectedScheme.Blocks.Count + 1,
            Kind = SchemeBlockKind.KeyPress,
            Name = "KeyPress Space",
            Parameters =
            {
                ["Key"] = "Space",
                ["HoldMilliseconds"] = "40"
            }
        };

        SelectedScheme.Blocks.Add(block);
        SelectedSchemeBlock = block;
        ReorderSchemeBlocks();
    }

    private void AddSchemeKeyDownBlock()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        var block = new SchemeBlock
        {
            Order = SelectedScheme.Blocks.Count + 1,
            Kind = SchemeBlockKind.KeyDown,
            Name = "KeyDown Space",
            Parameters =
            {
                ["Key"] = "Space"
            }
        };

        SelectedScheme.Blocks.Add(block);
        SelectedSchemeBlock = block;
        ReorderSchemeBlocks();
    }

    private void AddSchemeKeyUpBlock()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        var block = new SchemeBlock
        {
            Order = SelectedScheme.Blocks.Count + 1,
            Kind = SchemeBlockKind.KeyUp,
            Name = "KeyUp Space",
            Parameters =
            {
                ["Key"] = "Space"
            }
        };

        SelectedScheme.Blocks.Add(block);
        SelectedSchemeBlock = block;
        ReorderSchemeBlocks();
    }

    private void AddSchemeRepeatBlock()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        var block = new SchemeBlock
        {
            Order = SelectedScheme.Blocks.Count + 1,
            Kind = SchemeBlockKind.Repeat,
            Name = "Repeat 1x",
            Parameters =
            {
                ["Count"] = "1"
            }
        };

        SelectedScheme.Blocks.Add(block);
        SelectedSchemeBlock = block;
        ReorderSchemeBlocks();
    }

    private void AddSchemeIfTimeBlock()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        var block = new SchemeBlock
        {
            Order = SelectedScheme.Blocks.Count + 1,
            Kind = SchemeBlockKind.If,
            Name = "IF time >= 1000 ms",
            Parameters =
            {
                ["Condition"] = SchemeConditionKind.TimeElapsed.ToString(),
                ["Milliseconds"] = "1000"
            }
        };

        SelectedScheme.Blocks.Add(block);
        SelectedSchemeBlock = block;
        ReorderSchemeBlocks();
    }

    private void AddSchemeIfOcrContainsBlock()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        var block = new SchemeBlock
        {
            Order = SelectedScheme.Blocks.Count + 1,
            Kind = SchemeBlockKind.If,
            Name = "IF OCR contains",
            Parameters =
            {
                ["Condition"] = SchemeConditionKind.OcrContains.ToString(),
                ["X"] = "0",
                ["Y"] = "0",
                ["Width"] = "300",
                ["Height"] = "120",
                ["Language"] = "en-US",
                ["Contains"] = ""
            }
        };

        SelectedScheme.Blocks.Add(block);
        SelectedSchemeBlock = block;
        ReorderSchemeBlocks();
    }

    private void AddSchemeIfOcrSameBlock()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        var block = new SchemeBlock
        {
            Order = SelectedScheme.Blocks.Count + 1,
            Kind = SchemeBlockKind.If,
            Name = "IF OCR same in last 3000 ms",
            Parameters =
            {
                ["Condition"] = SchemeConditionKind.OcrSameInLast.ToString(),
                ["X"] = "0",
                ["Y"] = "0",
                ["Width"] = "300",
                ["Height"] = "120",
                ["Language"] = "en-US",
                ["DurationMilliseconds"] = "3000",
                ["PollMilliseconds"] = "500"
            }
        };

        SelectedScheme.Blocks.Add(block);
        SelectedSchemeBlock = block;
        ReorderSchemeBlocks();
    }

    private void AddSchemeIfScreenSameBlock()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        var block = new SchemeBlock
        {
            Order = SelectedScheme.Blocks.Count + 1,
            Kind = SchemeBlockKind.If,
            Name = "IF screen same in last 3000 ms",
            Parameters =
            {
                ["Condition"] = SchemeConditionKind.ScreenSameInLast.ToString(),
                ["X"] = "0",
                ["Y"] = "0",
                ["Width"] = "300",
                ["Height"] = "120",
                ["DurationMilliseconds"] = "3000",
                ["PollMilliseconds"] = "500",
                ["PixelTolerance"] = "15",
                ["MaxDifferencePercent"] = "0.50"
            }
        };

        SelectedScheme.Blocks.Add(block);
        SelectedSchemeBlock = block;
        ReorderSchemeBlocks();
    }

    private void AddSchemeSimpleBlock(SchemeBlockKind kind)
    {
        if (SelectedScheme is null)
        {
            return;
        }

        var block = new SchemeBlock
        {
            Order = SelectedScheme.Blocks.Count + 1,
            Kind = kind,
            Name = kind switch
            {
                SchemeBlockKind.Else => "ELSE",
                SchemeBlockKind.EndIf => "ENDIF",
                SchemeBlockKind.EndRepeat => "ENDREPEAT",
                _ => kind.ToString()
            }
        };

        SelectedScheme.Blocks.Add(block);
        SelectedSchemeBlock = block;
        ReorderSchemeBlocks();
    }

    private void MoveSchemeBlockUp()
    {
        if (SelectedScheme is null || SelectedSchemeBlock is null)
        {
            return;
        }

        var index = SelectedScheme.Blocks.IndexOf(SelectedSchemeBlock);

        if (index <= 0)
        {
            return;
        }

        SelectedScheme.Blocks.Move(index, index - 1);
        ReorderSchemeBlocks();

        if (SelectedProfile is not null)
        {
            SelectedProfile.UpdatedAt = DateTime.Now;
        }
    }

    private void MoveSchemeBlockDown()
    {
        if (SelectedScheme is null || SelectedSchemeBlock is null)
        {
            return;
        }

        var index = SelectedScheme.Blocks.IndexOf(SelectedSchemeBlock);

        if (index < 0 || index >= SelectedScheme.Blocks.Count - 1)
        {
            return;
        }

        SelectedScheme.Blocks.Move(index, index + 1);
        ReorderSchemeBlocks();

        if (SelectedProfile is not null)
        {
            SelectedProfile.UpdatedAt = DateTime.Now;
        }
    }

    private void DeleteSchemeBlock(SchemeBlock? blockToDelete = null)
    {
        if (SelectedScheme is null)
        {
            return;
        }

        var block = blockToDelete ?? SelectedSchemeBlock;

        if (block is null)
        {
            return;
        }

        SelectedScheme.Blocks.Remove(block);
        ReorderSchemeBlocks();

        if (SelectedSchemeBlock == block)
        {
            SelectedSchemeBlock = SelectedScheme.Blocks.FirstOrDefault();
        }

        if (SelectedProfile is not null)
        {
            SelectedProfile.UpdatedAt = DateTime.Now;
        }
    }

    private void ReorderSchemeBlocks()
    {
        if (SelectedScheme is null)
        {
            return;
        }

        for (var i = 0; i < SelectedScheme.Blocks.Count; i++)
        {
            SelectedScheme.Blocks[i].Order = i + 1;
        }
    }

    public void AddSchemeBlockFromPalette(string? blockKey)
    {
        if (string.IsNullOrWhiteSpace(blockKey))
        {
            return;
        }

        switch (blockKey)
        {
            case "RunMacro":
                AddSchemeRunMacroBlock();
                break;

            case "Delay":
                AddSchemeDelayBlock();
                break;

            case "KeyPress":
                AddSchemeKeyPressBlock();
                break;

            case "KeyDown":
                AddSchemeKeyDownBlock();
                break;

            case "KeyUp":
                AddSchemeKeyUpBlock();
                break;

            case "Repeat":
                AddSchemeRepeatBlock();
                break;

            case "IfTime":
                AddSchemeIfTimeBlock();
                break;

            case "IfOcrContains":
                AddSchemeIfOcrContainsBlock();
                break;

            case "IfOcrSame":
                AddSchemeIfOcrSameBlock();
                break;

            case "IfScreenSame":
                AddSchemeIfScreenSameBlock();
                break;

            case "Else":
                AddSchemeSimpleBlock(SchemeBlockKind.Else);
                break;

            case "EndIf":
                AddSchemeSimpleBlock(SchemeBlockKind.EndIf);
                break;

            case "EndRepeat":
                AddSchemeSimpleBlock(SchemeBlockKind.EndRepeat);
                break;
        }
    }

    private async Task SaveSelectedProfileAsync()
    {
        if (SelectedProfile is null)
        {
            _logger.Warning("Nie wybrano profilu do zapisu.");
            return;
        }

        await _profileRepository.SaveAsync(SelectedProfile);

        _logger.Info($"Zapisano profil: {SelectedProfile.Name}");
    }

    private async Task RunSelectedMacroAsync()
    {
        if (SelectedMacro is null)
        {
            _logger.Warning("Nie wybrano makra.");
            return;
        }

        await RunMacroAsync(SelectedMacro);
    }

    private async Task RunMacroAsync(MacroDefinition macro)
    {
        if (_macroCancellation is not null)
        {
            _logger.Warning("Makro już działa.");
            return;
        }

        if (_inputRecorder.IsRecording)
        {
            _logger.Warning("Najpierw zatrzymaj nagrywanie.");
            return;
        }

        _macroCancellation = new CancellationTokenSource();

        try
        {
            await _macroExecutor.ExecuteAsync(macro, _macroCancellation.Token);
        }
        finally
        {
            _macroCancellation.Dispose();
            _macroCancellation = null;
        }
    }

    private void StopMacro()
    {
        if (_macroCancellation is null)
        {
            _logger.Warning("Nie ma aktywnego makra do zatrzymania.");
            return;
        }

        _macroCancellation.Cancel();
    }

    private async Task StartRecordingAsync()
    {
        if (SelectedMacro is null)
        {
            _logger.Warning("Najpierw wybierz albo utwórz makro.");
            return;
        }

        if (_macroCancellation is not null)
        {
            _logger.Warning("Nie można nagrywać podczas działania makra.");
            return;
        }

        if (_inputRecorder.IsRecording)
        {
            _logger.Warning("Nagrywanie już działa.");
            return;
        }

        RecordingStatus = "Nagrywanie startuje za 2 sekundy...";
        _logger.Info("Nagrywanie startuje za 2 sekundy. Zatrzymanie: F12.");

        await Task.Delay(2000);

        _inputRecorder.Start();

        IsRecording = true;
        RecordingStatus = "Nagrywanie: aktywne. Stop = F12.";

        _logger.Info("Nagrywanie rozpoczęte.");
    }

    private void StopRecording()
    {
        if (!_inputRecorder.IsRecording)
        {
            RecordingStatus = "Nagrywanie: wyłączone";
            IsRecording = false;
            return;
        }

        _inputRecorder.Stop();

        IsRecording = false;
        RecordingStatus = "Nagrywanie: wyłączone";

        _logger.Info("Nagrywanie zatrzymane.");
    }

    private void OnInputRecorded(RecordedInputEvent recordedEvent)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (SelectedMacro is null)
            {
                return;
            }

            if (recordedEvent.DelayBeforeMilliseconds >= 100)
            {
                var delayStep = new MacroStep
                {
                    Order = SelectedMacro.Steps.Count + 1,
                    Name = $"Delay {recordedEvent.DelayBeforeMilliseconds} ms",
                    Kind = StepKind.Action,
                    Action = new ActionDefinition
                    {
                        Kind = ActionKind.Delay,
                        Parameters =
                        {
                            ["Milliseconds"] = recordedEvent.DelayBeforeMilliseconds.ToString()
                        }
                    }
                };

                SelectedMacro.Steps.Add(delayStep);
            }

            var actionStep = new MacroStep
            {
                Order = SelectedMacro.Steps.Count + 1,
                Name = recordedEvent.DisplayName,
                Kind = StepKind.Action,
                Action = recordedEvent.Action
            };

            SelectedMacro.Steps.Add(actionStep);
            ReorderSteps();

            SelectedStep = actionStep;

            _logger.Info(recordedEvent.DisplayName);
        });
    }

    private void OnGlobalMouseTriggered(GlobalMouseTriggerButton button)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            _ = RunBoundMouseMacroAsync(button);
        }));
    }

    private async Task RunBoundMouseMacroAsync(GlobalMouseTriggerButton button)
    {
        if (SelectedProfile is null)
        {
            return;
        }

        if (_inputRecorder.IsRecording)
        {
            return;
        }

        if (_macroCancellation is not null)
        {
            return;
        }

        var binding = SelectedProfile.TriggerBindings;

        var targetKind = button switch
        {
            GlobalMouseTriggerButton.Mouse4 => binding.Mouse4TargetKind,
            GlobalMouseTriggerButton.Mouse5 => binding.Mouse5TargetKind,
            _ => TriggerTargetKind.Macro
        };

        var runMode = button switch
        {
            GlobalMouseTriggerButton.Mouse4 => binding.Mouse4RunMode,
            GlobalMouseTriggerButton.Mouse5 => binding.Mouse5RunMode,
            _ => TriggerRunMode.Once
        };

        var repeatCount = button switch
        {
            GlobalMouseTriggerButton.Mouse4 => binding.Mouse4RepeatCount,
            GlobalMouseTriggerButton.Mouse5 => binding.Mouse5RepeatCount,
            _ => 1
        };

        repeatCount = Math.Max(1, repeatCount);

        MacroDefinition? macro = null;
        AutomationScheme? scheme = null;

        if (targetKind == TriggerTargetKind.Macro)
        {
            var macroId = button switch
            {
                GlobalMouseTriggerButton.Mouse4 => binding.Mouse4MacroId,
                GlobalMouseTriggerButton.Mouse5 => binding.Mouse5MacroId,
                _ => null
            };

            if (macroId is null)
            {
                return;
            }

            macro = SelectedProfile.Macros.FirstOrDefault(x => x.Id == macroId.Value);

            if (macro is null)
            {
                return;
            }
        }
        else
        {
            var schemeId = button switch
            {
                GlobalMouseTriggerButton.Mouse4 => binding.Mouse4SchemeId,
                GlobalMouseTriggerButton.Mouse5 => binding.Mouse5SchemeId,
                _ => null
            };

            if (schemeId is null)
            {
                return;
            }

            scheme = SelectedProfile.Schemes.FirstOrDefault(x => x.Id == schemeId.Value);

            if (scheme is null)
            {
                return;
            }
        }

        _macroCancellation = new CancellationTokenSource();

        try
        {
            switch (runMode)
            {
                case TriggerRunMode.Once:
                    await ExecuteBoundTargetOnceAsync(
                        targetKind,
                        macro,
                        scheme,
                        _macroCancellation.Token);
                    break;

                case TriggerRunMode.RepeatCount:
                    for (var i = 0; i < repeatCount; i++)
                    {
                        _macroCancellation.Token.ThrowIfCancellationRequested();

                        await ExecuteBoundTargetOnceAsync(
                            targetKind,
                            macro,
                            scheme,
                            _macroCancellation.Token);
                    }
                    break;

                case TriggerRunMode.LoopUntilStopped:
                    while (!_macroCancellation.Token.IsCancellationRequested)
                    {
                        await ExecuteBoundTargetOnceAsync(
                            targetKind,
                            macro,
                            scheme,
                            _macroCancellation.Token);
                    }
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // normalne zatrzymanie przez Stop
        }
        finally
        {
            _macroCancellation.Dispose();
            _macroCancellation = null;
        }
    }

    private async Task ExecuteBoundTargetOnceAsync(
        TriggerTargetKind targetKind,
        MacroDefinition? macro,
        AutomationScheme? scheme,
        CancellationToken cancellationToken)
    {
        if (targetKind == TriggerTargetKind.Macro)
        {
            if (macro is null)
            {
                return;
            }

            await _macroExecutor.ExecuteAsync(macro, cancellationToken);
            return;
        }

        if (scheme is null || SelectedProfile is null)
        {
            return;
        }

        await _schemeExecutor.ExecuteAsync(
            scheme,
            SelectedProfile.Macros,
            cancellationToken);
    }

    private async Task TestOcrAsync()
    {
        if (!TryBuildOcrRegion(out var region))
        {
            OcrStatus = "OCR: błędny region.";
            OcrLastText = "";
            return;
        }

        OcrStatus = $"OCR: czytam region {region}...";
        OcrLastText = "";

        try
        {
            var result = await _ocrService.ReadRegionAsync(
                region,
                OcrLanguage,
                CancellationToken.None);

            OcrLastText = string.IsNullOrWhiteSpace(result.Text)
                ? "(brak tekstu)"
                : result.Text;

            OcrStatus = "OCR: zakończono.";
        }
        catch (Exception ex)
        {
            OcrStatus = $"OCR: błąd - {ex.Message}";
            OcrLastText = "";
        }
    }

    private bool TryBuildOcrRegion(out ScreenRegion region)
    {
        region = new ScreenRegion();

        if (!int.TryParse(OcrX, out var x))
        {
            return false;
        }

        if (!int.TryParse(OcrY, out var y))
        {
            return false;
        }

        if (!int.TryParse(OcrWidth, out var width))
        {
            return false;
        }

        if (!int.TryParse(OcrHeight, out var height))
        {
            return false;
        }

        region = new ScreenRegion
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };

        return region.IsValid();
    }

    private MacroDefinition? FindMacroById(Guid? macroId)
    {
        if (macroId is null)
        {
            return null;
        }

        return CurrentMacros.FirstOrDefault(x => x.Id == macroId.Value);
    }

    private AutomationScheme? FindSchemeById(Guid? schemeId)
    {
        if (schemeId is null)
        {
            return null;
        }

        return CurrentSchemes.FirstOrDefault(x => x.Id == schemeId.Value);
    }

    private void ApplyMouseTriggerSettingsToService()
    {
        if (SelectedProfile is null)
        {
            _mouseTriggerService.BlockMouse4 = false;
            _mouseTriggerService.BlockMouse5 = false;
            return;
        }

        _mouseTriggerService.BlockMouse4 = SelectedProfile.TriggerBindings.BlockMouse4OriginalAction;
        _mouseTriggerService.BlockMouse5 = SelectedProfile.TriggerBindings.BlockMouse5OriginalAction;
    }

    private void RaiseMouseBindingPropertiesChanged()
    {
        OnPropertyChanged(nameof(Mouse4TargetKind));
        OnPropertyChanged(nameof(Mouse4AssignedMacro));
        OnPropertyChanged(nameof(Mouse4AssignedScheme));
        OnPropertyChanged(nameof(Mouse4RunMode));
        OnPropertyChanged(nameof(Mouse4RepeatCount));
        OnPropertyChanged(nameof(Mouse4MacroBindingVisibility));
        OnPropertyChanged(nameof(Mouse4SchemeBindingVisibility));
        OnPropertyChanged(nameof(Mouse4RepeatCountVisibility));
        OnPropertyChanged(nameof(BlockMouse4OriginalAction));

        OnPropertyChanged(nameof(Mouse5TargetKind));
        OnPropertyChanged(nameof(Mouse5AssignedMacro));
        OnPropertyChanged(nameof(Mouse5AssignedScheme));
        OnPropertyChanged(nameof(Mouse5RunMode));
        OnPropertyChanged(nameof(Mouse5RepeatCount));
        OnPropertyChanged(nameof(Mouse5MacroBindingVisibility));
        OnPropertyChanged(nameof(Mouse5SchemeBindingVisibility));
        OnPropertyChanged(nameof(Mouse5RepeatCountVisibility));
        OnPropertyChanged(nameof(BlockMouse5OriginalAction));
    }

    private void EnsureAction(MacroStep step)
    {
        step.Kind = StepKind.Action;

        step.Action ??= new ActionDefinition
        {
            Kind = ActionKind.Delay
        };
    }

    private void ApplyDefaultParameters(ActionDefinition action)
    {
        action.Parameters.Clear();

        switch (action.Kind)
        {
            case ActionKind.LogMessage:
                action.Parameters["Message"] = "Nowy log.";
                break;

            case ActionKind.Delay:
                action.Parameters["Milliseconds"] = "1000";
                break;

            case ActionKind.MoveMouse:
                action.Parameters["Mode"] = "Relative";
                action.Parameters["X"] = "50";
                action.Parameters["Y"] = "0";
                break;

            case ActionKind.MouseClick:
                action.Parameters["Button"] = "Left";
                action.Parameters["DownUpDelayMilliseconds"] = "40";
                break;

            case ActionKind.MouseDown:
            case ActionKind.MouseUp:
                action.Parameters["Button"] = "Left";
                break;

            case ActionKind.KeyPress:
                action.Parameters["Key"] = "Space";
                action.Parameters["HoldMilliseconds"] = "40";
                break;

            case ActionKind.KeyDown:
            case ActionKind.KeyUp:
                action.Parameters["Key"] = "Space";
                break;

            case ActionKind.RunMacro:
                break;
        }
    }

    private string GetParam(string key, string fallback = "")
    {
        if (SelectedStep?.Action is null)
        {
            return fallback;
        }

        return SelectedStep.Action.Parameters.TryGetValue(key, out var value)
            ? value
            : fallback;
    }

    private void SetParam(string key, string value)
    {
        if (SelectedStep is null)
        {
            return;
        }

        EnsureAction(SelectedStep);

        if (string.IsNullOrWhiteSpace(value))
        {
            SelectedStep.Action!.Parameters.Remove(key);
        }
        else
        {
            SelectedStep.Action!.Parameters[key] = value;
        }

        OnPropertyChanged();
    }

    private void RaiseStepEditorPropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedStepActionKind));

        OnPropertyChanged(nameof(DelayMillisecondsParam));
        OnPropertyChanged(nameof(DelayMinMillisecondsParam));
        OnPropertyChanged(nameof(DelayMaxMillisecondsParam));

        OnPropertyChanged(nameof(MoveModeParam));
        OnPropertyChanged(nameof(MoveXParam));
        OnPropertyChanged(nameof(MoveYParam));

        OnPropertyChanged(nameof(MouseButtonParam));
        OnPropertyChanged(nameof(ClickXParam));
        OnPropertyChanged(nameof(ClickYParam));
        OnPropertyChanged(nameof(ClickDownUpDelayMillisecondsParam));

        OnPropertyChanged(nameof(KeyParam));
        OnPropertyChanged(nameof(KeyModifiersParam));
        OnPropertyChanged(nameof(KeyHoldMillisecondsParam));

        RaiseWorkspaceVisibilityPropertiesChanged();
    }

    private string GetSchemeParam(string key, string fallback = "")
    {
        if (SelectedSchemeBlock is null)
        {
            return fallback;
        }

        return SelectedSchemeBlock.Parameters.TryGetValue(key, out var value)
            ? value
            : fallback;
    }

    private void SetSchemeParam(string key, string value)
    {
        if (SelectedSchemeBlock is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            SelectedSchemeBlock.Parameters.Remove(key);
        }
        else
        {
            SelectedSchemeBlock.Parameters[key] = value;
        }

        OnPropertyChanged();
    }

    private void ApplyDefaultSchemeBlockParameters(SchemeBlock block)
    {
        block.Parameters.Clear();
        block.MacroId = null;

        switch (block.Kind)
        {
            case SchemeBlockKind.RunMacro:
                block.Name = "Run Macro";
                block.MacroId = SelectedMacro?.Id;

                if (SelectedMacro is not null)
                {
                    block.Name = $"Run {SelectedMacro.Name}";
                }

                break;

            case SchemeBlockKind.Delay:
                block.Name = "Delay 1000 ms";
                block.Parameters["Milliseconds"] = "1000";
                break;

            case SchemeBlockKind.KeyPress:
                block.Name = "KeyPress Space";
                block.Parameters["Key"] = "Space";
                block.Parameters["HoldMilliseconds"] = "40";
                break;

            case SchemeBlockKind.KeyDown:
                block.Name = "KeyDown Space";
                block.Parameters["Key"] = "Space";
                break;

            case SchemeBlockKind.KeyUp:
                block.Name = "KeyUp Space";
                block.Parameters["Key"] = "Space";
                break;

            case SchemeBlockKind.Repeat:
                block.Name = "Repeat 1x";
                block.Parameters["Count"] = "1";
                break;

            case SchemeBlockKind.If:
                block.Name = "IF time >= 1000 ms";
                block.Parameters["Condition"] = SchemeConditionKind.TimeElapsed.ToString();
                block.Parameters["Milliseconds"] = "1000";
                break;

            case SchemeBlockKind.Else:
                block.Name = "ELSE";
                break;

            case SchemeBlockKind.EndIf:
                block.Name = "ENDIF";
                break;

            case SchemeBlockKind.EndRepeat:
                block.Name = "ENDREPEAT";
                break;
        }
    }

    private void SetWorkspaceMode(bool isSchemeWorkspace)
    {
        if (_isSchemeWorkspace == isSchemeWorkspace)
        {
            return;
        }

        _isSchemeWorkspace = isSchemeWorkspace;
        RaiseWorkspaceVisibilityPropertiesChanged();
    }

    private void RaiseWorkspaceVisibilityPropertiesChanged()
    {
        OnPropertyChanged(nameof(MacroWorkspaceVisibility));
        OnPropertyChanged(nameof(SchemeWorkspaceVisibility));
        OnPropertyChanged(nameof(StepEditorVisibility));
        OnPropertyChanged(nameof(SchemeBlockEditorVisibility));
        OnPropertyChanged(nameof(EmptyEditorVisibility));
    }

    private void RaiseSchemeBlockEditorPropertiesChanged()
    {
        OnPropertyChanged(nameof(SchemeBlockEditorVisibility));
        OnPropertyChanged(nameof(SchemeRunMacroEditorVisibility));
        OnPropertyChanged(nameof(SchemeDelayEditorVisibility));
        OnPropertyChanged(nameof(SchemeKeyEditorVisibility));
        OnPropertyChanged(nameof(SchemeKeyHoldEditorVisibility));
        OnPropertyChanged(nameof(SchemeRepeatEditorVisibility));
        OnPropertyChanged(nameof(SchemeIfEditorVisibility));
        OnPropertyChanged(nameof(SchemeIfTimeEditorVisibility));
        OnPropertyChanged(nameof(SchemeOcrRegionEditorVisibility));
        OnPropertyChanged(nameof(SchemeOcrContainsEditorVisibility));
        OnPropertyChanged(nameof(SchemeOcrSameEditorVisibility));
        OnPropertyChanged(nameof(SchemeScreenSameEditorVisibility));

        OnPropertyChanged(nameof(SelectedSchemeBlockName));
        OnPropertyChanged(nameof(SelectedSchemeBlockKind));
        OnPropertyChanged(nameof(SchemeBlockMacro));
        OnPropertyChanged(nameof(SchemeConditionParam));

        OnPropertyChanged(nameof(SchemeDelayMillisecondsParam));
        OnPropertyChanged(nameof(SchemeTimeMillisecondsParam));
        OnPropertyChanged(nameof(SchemeDelayMinMillisecondsParam));
        OnPropertyChanged(nameof(SchemeDelayMaxMillisecondsParam));
        OnPropertyChanged(nameof(SchemeKeyParam));
        OnPropertyChanged(nameof(SchemeKeyModifiersParam));
        OnPropertyChanged(nameof(SchemeKeyHoldMillisecondsParam));
        OnPropertyChanged(nameof(SchemeRepeatCountParam));

        OnPropertyChanged(nameof(SchemeOcrXParam));
        OnPropertyChanged(nameof(SchemeOcrYParam));
        OnPropertyChanged(nameof(SchemeOcrWidthParam));
        OnPropertyChanged(nameof(SchemeOcrHeightParam));
        OnPropertyChanged(nameof(SchemeOcrLanguageParam));
        OnPropertyChanged(nameof(SchemeOcrContainsParam));
        OnPropertyChanged(nameof(SchemeOcrDurationMillisecondsParam));
        OnPropertyChanged(nameof(SchemeOcrPollMillisecondsParam));

        RaiseWorkspaceVisibilityPropertiesChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
