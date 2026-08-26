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

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IProfileRepository _profileRepository;
    private readonly IPitLogger _logger;
    private readonly MacroExecutor _macroExecutor;
    private readonly IInputRecorder _inputRecorder;
    private readonly IGlobalMouseTriggerService _mouseTriggerService;
    private readonly IOcrService _ocrService;
    private readonly IScreenRegionService _screenRegionService;
    private readonly SchemeExecutor _schemeExecutor;

    private CancellationTokenSource? _macroCancellation;

    private bool _isSchemeWorkspace;

    private AutomationProfile? _selectedProfile;
    private MacroDefinition? _selectedMacro;
    private AutomationScheme? _selectedScheme;
    private SchemeBlock? _selectedSchemeBlock;
    private MacroStep? _selectedStep;

    private bool _isRecording;
    private string _recordingStatus = "Nagrywanie: wyłączone";

    private string _ocrX = "0";
    private string _ocrY = "0";
    private string _ocrWidth = "300";
    private string _ocrHeight = "120";
    private string _ocrLanguage = "en-US";
    private string _ocrStatus = "OCR: gotowy";
    private string _ocrLastText = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AutomationProfile> Profiles { get; } = new();

    public ObservableCollection<MacroDefinition> CurrentMacros { get; } = new();

    public ObservableCollection<AutomationScheme> CurrentSchemes { get; } = new();

    public ObservableCollection<ExecutionLogEntry> Logs { get; } = new();

    public IReadOnlyList<ActionKind> ActionKinds { get; } = Enum.GetValues<ActionKind>()
        .Where(x => x != ActionKind.LogMessage)
        .ToList();

    public IReadOnlyList<string> MouseMoveModes { get; } = Enum.GetNames<MouseMoveMode>().ToList();

    public IReadOnlyList<string> MouseButtons { get; } = Enum.GetNames<PIT.Core.Input.MouseButton>().ToList();

    public IReadOnlyList<TriggerTargetKind> TriggerTargetKinds { get; } =
        Enum.GetValues<TriggerTargetKind>().ToList();

    public IReadOnlyList<TriggerRunMode> TriggerRunModes { get; } =
        Enum.GetValues<TriggerRunMode>().ToList();

    public ICommand CreateSampleProfileCommand { get; }

    public ICommand SaveSelectedProfileCommand { get; }

    public ICommand AddProfileCommand { get; }

    public ICommand DeleteProfileCommand { get; }

    public ICommand AddMacroCommand { get; }

    public ICommand DeleteMacroCommand { get; }

    public ICommand AddSchemeCommand { get; }

    public ICommand DeleteSchemeCommand { get; }

    public ICommand AddStepCommand { get; }

    public ICommand DeleteStepCommand { get; }

    public ICommand MoveStepUpCommand { get; }

    public ICommand MoveStepDownCommand { get; }

    public ICommand RunSelectedMacroCommand { get; }

    public ICommand StopMacroCommand { get; }

    public ICommand StartRecordingCommand { get; }

    public ICommand StopRecordingCommand { get; }

    public ICommand TestOcrCommand { get; }

    public ICommand RunSelectedSchemeCommand { get; }

    public ICommand AddSchemeRunMacroBlockCommand { get; }

    public ICommand AddSchemeDelayBlockCommand { get; }

    public ICommand AddSchemeKeyPressBlockCommand { get; }

    public ICommand AddSchemeKeyDownBlockCommand { get; }

    public ICommand AddSchemeKeyUpBlockCommand { get; }

    public ICommand AddSchemeRepeatBlockCommand { get; }

    public ICommand AddSchemeIfTimeBlockCommand { get; }

    public ICommand AddSchemeIfOcrContainsBlockCommand { get; }

    public ICommand AddSchemeIfOcrSameBlockCommand { get; }

    public ICommand AddSchemeIfScreenSameBlockCommand { get; }

    public ICommand AddSchemeElseBlockCommand { get; }

    public ICommand AddSchemeEndIfBlockCommand { get; }

    public ICommand AddSchemeEndRepeatBlockCommand { get; }

    public ICommand DeleteSchemeBlockCommand { get; }

    public ICommand MoveSchemeBlockUpCommand { get; }

    public ICommand MoveSchemeBlockDownCommand { get; }

    public AutomationProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (_selectedProfile == value)
            {
                return;
            }

            _selectedProfile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedProfileName));

            RefreshCurrentMacrosAndSchemes();
            ApplyMouseTriggerSettingsToService();
            RaiseMouseBindingPropertiesChanged();
        }
    }

    public MacroDefinition? SelectedMacro
    {
        get => _selectedMacro;
        set
        {
            if (_selectedMacro == value)
            {
                return;
            }

            _selectedMacro = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedMacroName));

            SelectedStep = _selectedMacro?.Steps.FirstOrDefault();

            if (_selectedMacro is not null)
            {
                SelectedSchemeBlock = null;
                SetWorkspaceMode(isSchemeWorkspace: false);
            }
        }
    }

    public AutomationScheme? SelectedScheme
    {
        get => _selectedScheme;
        set
        {
            if (_selectedScheme == value)
            {
                return;
            }

            _selectedScheme = value;
            OnPropertyChanged();

            SelectedStep = null;
            SelectedSchemeBlock = _selectedScheme?.Blocks.FirstOrDefault();

            if (_selectedScheme is not null)
            {
                SetWorkspaceMode(isSchemeWorkspace: true);
            }
        }
    }

    public SchemeBlock? SelectedSchemeBlock
    {
        get => _selectedSchemeBlock;
        set
        {
            if (_selectedSchemeBlock == value)
            {
                return;
            }

            _selectedSchemeBlock = value;
            OnPropertyChanged();

            if (_selectedSchemeBlock is not null)
            {
                SelectedStep = null;
                SetWorkspaceMode(isSchemeWorkspace: true);
            }

            RaiseSchemeBlockEditorPropertiesChanged();
            RaiseWorkspaceVisibilityPropertiesChanged();
        }
    }

    public MacroStep? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (_selectedStep == value)
            {
                return;
            }

            _selectedStep = value;
            OnPropertyChanged();

            if (_selectedStep is not null)
            {
                SelectedSchemeBlock = null;
                SetWorkspaceMode(isSchemeWorkspace: false);
            }

            RaiseStepEditorPropertiesChanged();
            RaiseWorkspaceVisibilityPropertiesChanged();
        }
    }

    public string SelectedProfileName
    {
        get => SelectedProfile?.Name ?? "";
        set
        {
            if (SelectedProfile is null)
            {
                return;
            }

            if (SelectedProfile.Name == value)
            {
                return;
            }

            SelectedProfile.Name = value;
            SelectedProfile.UpdatedAt = DateTime.Now;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedProfile));
        }
    }

    public string SelectedMacroName
    {
        get => SelectedMacro?.Name ?? "";
        set
        {
            if (SelectedMacro is null)
            {
                return;
            }

            if (SelectedMacro.Name == value)
            {
                return;
            }

            SelectedMacro.Name = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedMacro));

            var current = SelectedMacro;
            var index = CurrentMacros.IndexOf(current);

            if (index >= 0)
            {
                CurrentMacros.RemoveAt(index);
                CurrentMacros.Insert(index, current);
                SelectedMacro = current;
            }

            RaiseMouseBindingPropertiesChanged();
        }
    }

    public TriggerTargetKind Mouse4TargetKind
    {
        get => SelectedProfile?.TriggerBindings.Mouse4TargetKind ?? TriggerTargetKind.Macro;
        set
        {
            if (SelectedProfile is null)
            {
                return;
            }

            SelectedProfile.TriggerBindings.Mouse4TargetKind = value;
            SelectedProfile.UpdatedAt = DateTime.Now;

            OnPropertyChanged();
            RaiseMouseBindingPropertiesChanged();
        }
    }

    public MacroDefinition? Mouse4AssignedMacro
    {
        get => FindMacroById(SelectedProfile?.TriggerBindings.Mouse4MacroId);
        set
        {
            if (SelectedProfile is null)
            {
                return;
            }

            SelectedProfile.TriggerBindings.Mouse4MacroId = value?.Id;
            SelectedProfile.UpdatedAt = DateTime.Now;

            OnPropertyChanged();
        }
    }

    public AutomationScheme? Mouse4AssignedScheme
    {
        get => FindSchemeById(SelectedProfile?.TriggerBindings.Mouse4SchemeId);
        set
        {
            if (SelectedProfile is null)
            {
                return;
            }

            SelectedProfile.TriggerBindings.Mouse4SchemeId = value?.Id;
            SelectedProfile.UpdatedAt = DateTime.Now;

            OnPropertyChanged();
        }
    }

    public TriggerRunMode Mouse4RunMode
    {
        get => SelectedProfile?.TriggerBindings.Mouse4RunMode ?? TriggerRunMode.Once;
        set
        {
            if (SelectedProfile is null)
            {
                return;
            }

            SelectedProfile.TriggerBindings.Mouse4RunMode = value;
            SelectedProfile.UpdatedAt = DateTime.Now;

            OnPropertyChanged();
            RaiseMouseBindingPropertiesChanged();
        }
    }

    public string Mouse4RepeatCount
    {
        get => (SelectedProfile?.TriggerBindings.Mouse4RepeatCount ?? 1).ToString();
        set
        {
            if (SelectedProfile is null)
            {
                return;
            }

            if (!int.TryParse(value, out var parsed))
            {
                return;
            }

            SelectedProfile.TriggerBindings.Mouse4RepeatCount = Math.Max(1, parsed);
            SelectedProfile.UpdatedAt = DateTime.Now;

            OnPropertyChanged();
        }
    }

    public Visibility Mouse4MacroBindingVisibility =>
        Mouse4TargetKind == TriggerTargetKind.Macro
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility Mouse4SchemeBindingVisibility =>
        Mouse4TargetKind == TriggerTargetKind.Scheme
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility Mouse4RepeatCountVisibility =>
        Mouse4RunMode == TriggerRunMode.RepeatCount
            ? Visibility.Visible
            : Visibility.Collapsed;

    public TriggerTargetKind Mouse5TargetKind
    {
        get => SelectedProfile?.TriggerBindings.Mouse5TargetKind ?? TriggerTargetKind.Macro;
        set
        {
            if (SelectedProfile is null)
            {
                return;
            }

            SelectedProfile.TriggerBindings.Mouse5TargetKind = value;
            SelectedProfile.UpdatedAt = DateTime.Now;

            OnPropertyChanged();
            RaiseMouseBindingPropertiesChanged();
        }
    }

    public MacroDefinition? Mouse5AssignedMacro
    {
        get => FindMacroById(SelectedProfile?.TriggerBindings.Mouse5MacroId);
        set
        {
            if (SelectedProfile is null)
            {
                return;
            }

            SelectedProfile.TriggerBindings.Mouse5MacroId = value?.Id;
            SelectedProfile.UpdatedAt = DateTime.Now;

            OnPropertyChanged();
        }
    }

    public AutomationScheme? Mouse5AssignedScheme
    {
        get => FindSchemeById(SelectedProfile?.TriggerBindings.Mouse5SchemeId);
        set
        {
            if (SelectedProfile is null)
            {
                return;
            }

            SelectedProfile.TriggerBindings.Mouse5SchemeId = value?.Id;
            SelectedProfile.UpdatedAt = DateTime.Now;

            OnPropertyChanged();
        }
    }

    public TriggerRunMode Mouse5RunMode
    {
        get => SelectedProfile?.TriggerBindings.Mouse5RunMode ?? TriggerRunMode.Once;
        set
        {
            if (SelectedProfile is null)
            {
                return;
            }

            SelectedProfile.TriggerBindings.Mouse5RunMode = value;
            SelectedProfile.UpdatedAt = DateTime.Now;

            OnPropertyChanged();
            RaiseMouseBindingPropertiesChanged();
        }
    }

    public string Mouse5RepeatCount
    {
        get => (SelectedProfile?.TriggerBindings.Mouse5RepeatCount ?? 1).ToString();
        set
        {
            if (SelectedProfile is null)
            {
                return;
            }

            if (!int.TryParse(value, out var parsed))
            {
                return;
            }

            SelectedProfile.TriggerBindings.Mouse5RepeatCount = Math.Max(1, parsed);
            SelectedProfile.UpdatedAt = DateTime.Now;

            OnPropertyChanged();
        }
    }

    public Visibility Mouse5MacroBindingVisibility =>
        Mouse5TargetKind == TriggerTargetKind.Macro
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility Mouse5SchemeBindingVisibility =>
        Mouse5TargetKind == TriggerTargetKind.Scheme
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility Mouse5RepeatCountVisibility =>
        Mouse5RunMode == TriggerRunMode.RepeatCount
            ? Visibility.Visible
            : Visibility.Collapsed;

    public bool BlockMouse4OriginalAction
    {
        get => SelectedProfile?.TriggerBindings.BlockMouse4OriginalAction ?? false;
        set
        {
            if (SelectedProfile is null)
            {
                return;
            }

            SelectedProfile.TriggerBindings.BlockMouse4OriginalAction = value;
            SelectedProfile.UpdatedAt = DateTime.Now;

            ApplyMouseTriggerSettingsToService();
            OnPropertyChanged();
        }
    }

    public bool BlockMouse5OriginalAction
    {
        get => SelectedProfile?.TriggerBindings.BlockMouse5OriginalAction ?? false;
        set
        {
            if (SelectedProfile is null)
            {
                return;
            }

            SelectedProfile.TriggerBindings.BlockMouse5OriginalAction = value;
            SelectedProfile.UpdatedAt = DateTime.Now;

            ApplyMouseTriggerSettingsToService();
            OnPropertyChanged();
        }
    }

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (_isRecording == value)
            {
                return;
            }

            _isRecording = value;
            OnPropertyChanged();
        }
    }

    public string RecordingStatus
    {
        get => _recordingStatus;
        private set
        {
            if (_recordingStatus == value)
            {
                return;
            }

            _recordingStatus = value;
            OnPropertyChanged();
        }
    }

    public Visibility MacroWorkspaceVisibility =>
        !_isSchemeWorkspace
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SchemeWorkspaceVisibility =>
        _isSchemeWorkspace
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility StepEditorVisibility =>
        !_isSchemeWorkspace && SelectedStep is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SchemeBlockEditorVisibility =>
        _isSchemeWorkspace && SelectedSchemeBlock is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility EmptyEditorVisibility =>
        SelectedStep is null && SelectedSchemeBlock is null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string OcrX
    {
        get => _ocrX;
        set
        {
            if (_ocrX == value)
            {
                return;
            }

            _ocrX = value;
            OnPropertyChanged();
        }
    }

    public string OcrY
    {
        get => _ocrY;
        set
        {
            if (_ocrY == value)
            {
                return;
            }

            _ocrY = value;
            OnPropertyChanged();
        }
    }

    public string OcrWidth
    {
        get => _ocrWidth;
        set
        {
            if (_ocrWidth == value)
            {
                return;
            }

            _ocrWidth = value;
            OnPropertyChanged();
        }
    }

    public string OcrHeight
    {
        get => _ocrHeight;
        set
        {
            if (_ocrHeight == value)
            {
                return;
            }

            _ocrHeight = value;
            OnPropertyChanged();
        }
    }

    public string OcrLanguage
    {
        get => _ocrLanguage;
        set
        {
            if (_ocrLanguage == value)
            {
                return;
            }

            _ocrLanguage = value;
            OnPropertyChanged();
        }
    }

    public string OcrStatus
    {
        get => _ocrStatus;
        private set
        {
            if (_ocrStatus == value)
            {
                return;
            }

            _ocrStatus = value;
            OnPropertyChanged();
        }
    }

    public string OcrLastText
    {
        get => _ocrLastText;
        private set
        {
            if (_ocrLastText == value)
            {
                return;
            }

            _ocrLastText = value;
            OnPropertyChanged();
        }
    }

    public ActionKind SelectedStepActionKind
    {
        get => SelectedStep?.Action?.Kind ?? ActionKind.Delay;
        set
        {
            if (SelectedStep is null)
            {
                return;
            }

            EnsureAction(SelectedStep);

            if (SelectedStep.Action!.Kind == value)
            {
                return;
            }

            SelectedStep.Kind = StepKind.Action;
            SelectedStep.Action.Kind = value;
            ApplyDefaultParameters(SelectedStep.Action);

            OnPropertyChanged();
            RaiseStepEditorPropertiesChanged();
        }
    }

    public string DelayMillisecondsParam
    {
        get => GetParam("Milliseconds");
        set => SetParam("Milliseconds", value);
    }

    public string DelayMinMillisecondsParam
    {
        get => GetParam("MinMilliseconds");
        set => SetParam("MinMilliseconds", value);
    }

    public string DelayMaxMillisecondsParam
    {
        get => GetParam("MaxMilliseconds");
        set => SetParam("MaxMilliseconds", value);
    }

    public string MoveModeParam
    {
        get => GetParam("Mode", "Relative");
        set => SetParam("Mode", value);
    }

    public string MoveXParam
    {
        get => GetParam("X");
        set => SetParam("X", value);
    }

    public string MoveYParam
    {
        get => GetParam("Y");
        set => SetParam("Y", value);
    }

    public string MouseButtonParam
    {
        get => GetParam("Button", "Left");
        set => SetParam("Button", value);
    }

    public string ClickXParam
    {
        get => GetParam("X");
        set => SetParam("X", value);
    }

    public string ClickYParam
    {
        get => GetParam("Y");
        set => SetParam("Y", value);
    }

    public string ClickDownUpDelayMillisecondsParam
    {
        get => GetParam("DownUpDelayMilliseconds", "40");
        set => SetParam("DownUpDelayMilliseconds", value);
    }

    public string KeyParam
    {
        get => GetParam("Key");
        set => SetParam("Key", value);
    }

    public string KeyModifiersParam
    {
        get => GetParam("Modifiers");
        set => SetParam("Modifiers", value);
    }

    public string KeyHoldMillisecondsParam
    {
        get => GetParam("HoldMilliseconds", "40");
        set => SetParam("HoldMilliseconds", value);
    }

    public string SelectedSchemeBlockName
    {
        get => SelectedSchemeBlock?.Name ?? "";
        set
        {
            if (SelectedSchemeBlock is null)
            {
                return;
            }

            if (SelectedSchemeBlock.Name == value)
            {
                return;
            }

            SelectedSchemeBlock.Name = value;
            OnPropertyChanged();
        }
    }

    public SchemeBlockKind SelectedSchemeBlockKind
    {
        get => SelectedSchemeBlock?.Kind ?? SchemeBlockKind.Delay;
        set
        {
            if (SelectedSchemeBlock is null)
            {
                return;
            }

            if (SelectedSchemeBlock.Kind == value)
            {
                return;
            }

            SelectedSchemeBlock.Kind = value;
            ApplyDefaultSchemeBlockParameters(SelectedSchemeBlock);

            OnPropertyChanged();
            RaiseSchemeBlockEditorPropertiesChanged();
        }
    }

    public IReadOnlyList<SchemeBlockKind> SchemeBlockKinds { get; } =
        Enum.GetValues<SchemeBlockKind>().ToList();

    public IReadOnlyList<SchemeConditionKind> SchemeConditionKinds { get; } =
        Enum.GetValues<SchemeConditionKind>().ToList();

    public MacroDefinition? SchemeBlockMacro
    {
        get
        {
            if (SelectedSchemeBlock?.MacroId is null)
            {
                return null;
            }

            return CurrentMacros.FirstOrDefault(x => x.Id == SelectedSchemeBlock.MacroId.Value);
        }
        set
        {
            if (SelectedSchemeBlock is null)
            {
                return;
            }

            SelectedSchemeBlock.MacroId = value?.Id;

            if (SelectedSchemeBlock.Kind == SchemeBlockKind.RunMacro)
            {
                SelectedSchemeBlock.Name = value is null
                    ? "Run Macro"
                    : $"Run {value.Name}";
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSchemeBlockName));
        }
    }

    public SchemeConditionKind SchemeConditionParam
    {
        get
        {
            var conditionText = GetSchemeParam("Condition", SchemeConditionKind.TimeElapsed.ToString());

            return Enum.TryParse<SchemeConditionKind>(conditionText, ignoreCase: true, out var condition)
                ? condition
                : SchemeConditionKind.TimeElapsed;
        }
        set
        {
            SetSchemeParam("Condition", value.ToString());

            if (SelectedSchemeBlock is not null)
            {
                SelectedSchemeBlock.Name = value switch
                {
                    SchemeConditionKind.TimeElapsed => "IF time",
                    SchemeConditionKind.OcrContains => "IF OCR contains",
                    SchemeConditionKind.OcrSameInLast => "IF OCR same in last",
                    SchemeConditionKind.ScreenSameInLast => "IF screen same in last",
                    _ => "IF"
                };
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSchemeBlockName));
            RaiseSchemeBlockEditorPropertiesChanged();
        }
    }

    public string SchemeDelayMillisecondsParam
    {
        get => GetSchemeParam("Milliseconds");
        set => SetSchemeParam("Milliseconds", value);
    }

    public string SchemeTimeMillisecondsParam
    {
        get => GetSchemeParam("Milliseconds", "1000");
        set => SetSchemeParam("Milliseconds", value);
    }

    public string SchemeDelayMinMillisecondsParam
    {
        get => GetSchemeParam("MinMilliseconds");
        set => SetSchemeParam("MinMilliseconds", value);
    }

    public string SchemeDelayMaxMillisecondsParam
    {
        get => GetSchemeParam("MaxMilliseconds");
        set => SetSchemeParam("MaxMilliseconds", value);
    }

    public string SchemeKeyParam
    {
        get => GetSchemeParam("Key", "Space");
        set => SetSchemeParam("Key", value);
    }

    public string SchemeKeyModifiersParam
    {
        get => GetSchemeParam("Modifiers");
        set => SetSchemeParam("Modifiers", value);
    }

    public string SchemeKeyHoldMillisecondsParam
    {
        get => GetSchemeParam("HoldMilliseconds", "40");
        set => SetSchemeParam("HoldMilliseconds", value);
    }

    public string SchemeRepeatCountParam
    {
        get => GetSchemeParam("Count", "1");
        set => SetSchemeParam("Count", value);
    }

    public string SchemeOcrXParam
    {
        get => GetSchemeParam("X");
        set => SetSchemeParam("X", value);
    }

    public string SchemeOcrYParam
    {
        get => GetSchemeParam("Y");
        set => SetSchemeParam("Y", value);
    }

    public string SchemeOcrWidthParam
    {
        get => GetSchemeParam("Width");
        set => SetSchemeParam("Width", value);
    }

    public string SchemeOcrHeightParam
    {
        get => GetSchemeParam("Height");
        set => SetSchemeParam("Height", value);
    }

    public string SchemeOcrLanguageParam
    {
        get => GetSchemeParam("Language", "en-US");
        set => SetSchemeParam("Language", value);
    }

    public string SchemeOcrContainsParam
    {
        get => GetSchemeParam("Contains");
        set => SetSchemeParam("Contains", value);
    }

    public string SchemeOcrDurationMillisecondsParam
    {
        get => GetSchemeParam("DurationMilliseconds", "3000");
        set => SetSchemeParam("DurationMilliseconds", value);
    }

    public string SchemeOcrPollMillisecondsParam
    {
        get => GetSchemeParam("PollMilliseconds", "500");
        set => SetSchemeParam("PollMilliseconds", value);
    }

    public Visibility SchemeRunMacroEditorVisibility =>
        SelectedSchemeBlock?.Kind == SchemeBlockKind.RunMacro
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SchemeDelayEditorVisibility =>
        SelectedSchemeBlock?.Kind == SchemeBlockKind.Delay
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SchemeKeyEditorVisibility =>
        SelectedSchemeBlock?.Kind is SchemeBlockKind.KeyPress or SchemeBlockKind.KeyDown or SchemeBlockKind.KeyUp
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SchemeKeyHoldEditorVisibility =>
        SelectedSchemeBlock?.Kind == SchemeBlockKind.KeyPress
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SchemeRepeatEditorVisibility =>
        SelectedSchemeBlock?.Kind == SchemeBlockKind.Repeat
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SchemeIfEditorVisibility =>
        SelectedSchemeBlock?.Kind == SchemeBlockKind.If
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SchemeIfTimeEditorVisibility =>
        SelectedSchemeBlock?.Kind == SchemeBlockKind.If
        && SchemeConditionParam == SchemeConditionKind.TimeElapsed
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SchemeOcrRegionEditorVisibility =>
        SelectedSchemeBlock?.Kind == SchemeBlockKind.If
        && (SchemeConditionParam == SchemeConditionKind.OcrContains
            || SchemeConditionParam == SchemeConditionKind.OcrSameInLast)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SchemeOcrContainsEditorVisibility =>
        SelectedSchemeBlock?.Kind == SchemeBlockKind.If
        && SchemeConditionParam == SchemeConditionKind.OcrContains
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SchemeOcrSameEditorVisibility =>
        SelectedSchemeBlock?.Kind == SchemeBlockKind.If
        && SchemeConditionParam == SchemeConditionKind.OcrSameInLast
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SchemeScreenSameEditorVisibility =>
        SelectedSchemeBlock?.Kind == SchemeBlockKind.If
        && SchemeConditionParam == SchemeConditionKind.ScreenSameInLast
            ? Visibility.Visible
            : Visibility.Collapsed;

    public MainViewModel()
    {
        _profileRepository = new JsonProfileRepository();
        _logger = new InMemoryPitLogger();
        _ocrService = new WindowsOcrService();
        _screenRegionService = new WindowsScreenRegionService();

        _inputRecorder = new WindowsInputRecorder();
        _inputRecorder.InputRecorded += OnInputRecorded;
        _inputRecorder.StopRequested += () =>
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(StopRecording));
        };

        _mouseTriggerService = new WindowsGlobalMouseTriggerService();
        _mouseTriggerService.Triggered += OnGlobalMouseTriggered;

        try
        {
            _mouseTriggerService.Start();
        }
        catch (Exception ex)
        {
            _logger.Warning($"Nie udało się uruchomić Mouse4/Mouse5 hooka: {ex.Message}");
        }

        IInputService inputService = new NativeInputService();

        var actionDispatcher = new ActionDispatcher(
            new IActionHandler[]
            {
                new LogMessageActionHandler(),
                new DelayActionHandler(),
                new KeyPressActionHandler(inputService),
                new MoveMouseActionHandler(inputService),
                new MouseClickActionHandler(inputService)
            },
            _logger);

        _macroExecutor = new MacroExecutor(_logger, actionDispatcher);

        _schemeExecutor = new SchemeExecutor(
            _logger,
            _macroExecutor,
            _ocrService,
            _screenRegionService);

        _logger.EntryAdded += entry =>
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                Logs.Add(entry);
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => Logs.Add(entry));
            }
        };

        CreateSampleProfileCommand = new RelayCommand(_ => CreateSampleProfile());
        SaveSelectedProfileCommand = new AsyncRelayCommand(SaveSelectedProfileAsync);

        AddProfileCommand = new RelayCommand(_ => AddProfile());
        DeleteProfileCommand = new AsyncRelayCommand(DeleteSelectedProfileAsync);

        AddMacroCommand = new RelayCommand(_ => AddMacro());
        DeleteMacroCommand = new RelayCommand(parameter => DeleteMacro(parameter as MacroDefinition));

        AddSchemeCommand = new RelayCommand(_ => AddScheme());
        DeleteSchemeCommand = new RelayCommand(parameter => DeleteScheme(parameter as AutomationScheme));

        AddStepCommand = new RelayCommand(_ => AddStep());
        DeleteStepCommand = new RelayCommand(parameter => DeleteStep(parameter as MacroStep));
        MoveStepUpCommand = new RelayCommand(_ => MoveStepUp());
        MoveStepDownCommand = new RelayCommand(_ => MoveStepDown());

        RunSelectedMacroCommand = new AsyncRelayCommand(RunSelectedMacroAsync);
        StopMacroCommand = new RelayCommand(_ => StopMacro());

        StartRecordingCommand = new AsyncRelayCommand(StartRecordingAsync);
        StopRecordingCommand = new RelayCommand(_ => StopRecording());

        TestOcrCommand = new AsyncRelayCommand(TestOcrAsync);

        RunSelectedSchemeCommand = new AsyncRelayCommand(RunSelectedSchemeAsync);

        AddSchemeRunMacroBlockCommand = new RelayCommand(_ => AddSchemeRunMacroBlock());
        AddSchemeDelayBlockCommand = new RelayCommand(_ => AddSchemeDelayBlock());
        AddSchemeKeyPressBlockCommand = new RelayCommand(_ => AddSchemeKeyPressBlock());
        AddSchemeKeyDownBlockCommand = new RelayCommand(_ => AddSchemeKeyDownBlock());
        AddSchemeKeyUpBlockCommand = new RelayCommand(_ => AddSchemeKeyUpBlock());
        AddSchemeRepeatBlockCommand = new RelayCommand(_ => AddSchemeRepeatBlock());
        AddSchemeIfTimeBlockCommand = new RelayCommand(_ => AddSchemeIfTimeBlock());
        AddSchemeIfOcrContainsBlockCommand = new RelayCommand(_ => AddSchemeIfOcrContainsBlock());
        AddSchemeIfOcrSameBlockCommand = new RelayCommand(_ => AddSchemeIfOcrSameBlock());
        AddSchemeIfScreenSameBlockCommand = new RelayCommand(_ => AddSchemeIfScreenSameBlock());
        AddSchemeElseBlockCommand = new RelayCommand(_ => AddSchemeSimpleBlock(SchemeBlockKind.Else));
        AddSchemeEndIfBlockCommand = new RelayCommand(_ => AddSchemeSimpleBlock(SchemeBlockKind.EndIf));
        AddSchemeEndRepeatBlockCommand = new RelayCommand(_ => AddSchemeSimpleBlock(SchemeBlockKind.EndRepeat));
        DeleteSchemeBlockCommand = new RelayCommand(parameter => DeleteSchemeBlock(parameter as SchemeBlock));
        MoveSchemeBlockUpCommand = new RelayCommand(_ => MoveSchemeBlockUp());
        MoveSchemeBlockDownCommand = new RelayCommand(_ => MoveSchemeBlockDown());

        _ = LoadProfilesAsync();
    }

    private async Task LoadProfilesAsync()
    {
        Profiles.Clear();

        var profiles = await _profileRepository.LoadAllAsync();

        foreach (var profile in profiles)
        {
            profile.TriggerBindings ??= new MacroTriggerBindings();
            profile.Schemes ??= new List<AutomationScheme>();

            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.FirstOrDefault();

        if (Profiles.Count == 0)
        {
            _logger.Info("Brak zapisanych profili. Utwórz profil testowy.");
        }
        else
        {
            _logger.Info($"Załadowano profile: {Profiles.Count}");
        }
    }

    private void RefreshCurrentMacrosAndSchemes()
    {
        CurrentMacros.Clear();
        CurrentSchemes.Clear();

        if (SelectedProfile is null)
        {
            SelectedMacro = null;
            SelectedScheme = null;
            return;
        }

        SelectedProfile.Schemes ??= new List<AutomationScheme>();

        foreach (var macro in SelectedProfile.Macros)
        {
            CurrentMacros.Add(macro);
        }

        foreach (var scheme in SelectedProfile.Schemes)
        {
            CurrentSchemes.Add(scheme);
        }

        SelectedMacro = CurrentMacros.FirstOrDefault();
        SelectedScheme = CurrentSchemes.FirstOrDefault();

        RaiseMouseBindingPropertiesChanged();
    }

    private void CreateSampleProfile()
    {
        var profile = new AutomationProfile
        {
            Name = "PIT - profil startowy"
        };

        var logMacro = new MacroDefinition
        {
            Name = "Test: delay"
        };

        logMacro.Steps.Add(new MacroStep
        {
            Order = 1,
            Name = "Delay 800 ms",
            Kind = StepKind.Action,
            Action = new ActionDefinition
            {
                Kind = ActionKind.Delay,
                Parameters =
                {
                    ["Milliseconds"] = "800"
                }
            }
        });

        var mouseMacro = new MacroDefinition
        {
            Name = "Test: mysz w prawo i powrót"
        };

        mouseMacro.Steps.Add(new MacroStep
        {
            Order = 1,
            Name = "Mysz +80 px",
            Kind = StepKind.Action,
            Action = new ActionDefinition
            {
                Kind = ActionKind.MoveMouse,
                Parameters =
                {
                    ["Mode"] = "Relative",
                    ["X"] = "80",
                    ["Y"] = "0"
                }
            }
        });

        mouseMacro.Steps.Add(new MacroStep
        {
            Order = 2,
            Name = "Delay 300 ms",
            Kind = StepKind.Action,
            Action = new ActionDefinition
            {
                Kind = ActionKind.Delay,
                Parameters =
                {
                    ["Milliseconds"] = "300"
                }
            }
        });

        mouseMacro.Steps.Add(new MacroStep
        {
            Order = 3,
            Name = "Mysz -80 px",
            Kind = StepKind.Action,
            Action = new ActionDefinition
            {
                Kind = ActionKind.MoveMouse,
                Parameters =
                {
                    ["Mode"] = "Relative",
                    ["X"] = "-80",
                    ["Y"] = "0"
                }
            }
        });

        var recordingMacro = new MacroDefinition
        {
            Name = "Nowe nagrywane makro"
        };

        var sampleScheme = new AutomationScheme
        {
            Name = "Schemat testowy"
        };

        profile.Macros.Add(logMacro);
        profile.Macros.Add(mouseMacro);
        profile.Macros.Add(recordingMacro);

        profile.Schemes.Add(sampleScheme);

        profile.TriggerBindings.Mouse4TargetKind = TriggerTargetKind.Macro;
        profile.TriggerBindings.Mouse4MacroId = mouseMacro.Id;
        profile.TriggerBindings.Mouse4RunMode = TriggerRunMode.Once;
        profile.TriggerBindings.Mouse4RepeatCount = 1;

        profile.TriggerBindings.Mouse5TargetKind = TriggerTargetKind.Macro;
        profile.TriggerBindings.Mouse5MacroId = recordingMacro.Id;
        profile.TriggerBindings.Mouse5RunMode = TriggerRunMode.Once;
        profile.TriggerBindings.Mouse5RepeatCount = 1;

        Profiles.Add(profile);
        SelectedProfile = profile;
        SelectedMacro = recordingMacro;
        SelectedScheme = sampleScheme;

        _logger.Info("Utworzono profil startowy.");
    }

    private void AddProfile()
    {
        var profileNumber = Profiles.Count + 1;

        var profile = new AutomationProfile
        {
            Name = $"Nowy profil {profileNumber}"
        };

        var macro = new MacroDefinition
        {
            Name = "Nowe makro 1"
        };

        profile.Macros.Add(macro);

        Profiles.Add(profile);
        SelectedProfile = profile;
        SelectedMacro = macro;

        _logger.Info($"Dodano profil: {profile.Name}");
    }

    private async Task DeleteSelectedProfileAsync()
    {
        if (SelectedProfile is null)
        {
            _logger.Warning("Nie wybrano profilu do usunięcia.");
            return;
        }

        var profile = SelectedProfile;

        await _profileRepository.DeleteAsync(profile);

        Profiles.Remove(profile);

        SelectedProfile = Profiles.FirstOrDefault();

        _logger.Info($"Usunięto profil: {profile.Name}");
    }

    private void AddMacro()
    {
        if (SelectedProfile is null)
        {
            _logger.Warning("Najpierw wybierz albo utwórz profil.");
            return;
        }

        var macro = new MacroDefinition
        {
            Name = $"Nowe makro {SelectedProfile.Macros.Count + 1}"
        };

        SelectedProfile.Macros.Add(macro);
        CurrentMacros.Add(macro);

        SelectedMacro = macro;
        RaiseMouseBindingPropertiesChanged();

        SelectedProfile.UpdatedAt = DateTime.Now;

        _logger.Info($"Dodano makro: {macro.Name}");
    }

    private void DeleteMacro(MacroDefinition? macroToDelete = null)
    {
        if (SelectedProfile is null)
        {
            _logger.Warning("Nie wybrano profilu.");
            return;
        }

        var macro = macroToDelete ?? SelectedMacro;

        if (macro is null)
        {
            _logger.Warning("Nie wybrano makra do usunięcia.");
            return;
        }

        if (SelectedProfile.TriggerBindings.Mouse4MacroId == macro.Id)
        {
            SelectedProfile.TriggerBindings.Mouse4MacroId = null;
        }

        if (SelectedProfile.TriggerBindings.Mouse5MacroId == macro.Id)
        {
            SelectedProfile.TriggerBindings.Mouse5MacroId = null;
        }

        SelectedProfile.Macros.Remove(macro);
        CurrentMacros.Remove(macro);

        if (SelectedMacro == macro)
        {
            SelectedMacro = CurrentMacros.FirstOrDefault();
        }

        RaiseMouseBindingPropertiesChanged();

        SelectedProfile.UpdatedAt = DateTime.Now;

        _logger.Info($"Usunięto makro: {macro.Name}");
    }

    private void AddScheme()
    {
        if (SelectedProfile is null)
        {
            _logger.Warning("Najpierw wybierz albo utwórz profil.");
            return;
        }

        SelectedProfile.Schemes ??= new List<AutomationScheme>();

        var scheme = new AutomationScheme
        {
            Name = $"Nowy schemat {SelectedProfile.Schemes.Count + 1}"
        };

        SelectedProfile.Schemes.Add(scheme);
        CurrentSchemes.Add(scheme);

        SelectedScheme = scheme;
        SelectedProfile.UpdatedAt = DateTime.Now;

        _logger.Info($"Dodano schemat: {scheme.Name}");
    }

    private void DeleteScheme(AutomationScheme? schemeToDelete = null)
    {
        if (SelectedProfile is null)
        {
            _logger.Warning("Nie wybrano profilu.");
            return;
        }

        var scheme = schemeToDelete ?? SelectedScheme;

        if (scheme is null)
        {
            _logger.Warning("Nie wybrano schematu do usunięcia.");
            return;
        }

        if (SelectedProfile.TriggerBindings.Mouse4SchemeId == scheme.Id)
        {
            SelectedProfile.TriggerBindings.Mouse4SchemeId = null;
        }

        if (SelectedProfile.TriggerBindings.Mouse5SchemeId == scheme.Id)
        {
            SelectedProfile.TriggerBindings.Mouse5SchemeId = null;
        }

        SelectedProfile.Schemes.Remove(scheme);
        CurrentSchemes.Remove(scheme);

        if (SelectedScheme == scheme)
        {
            SelectedScheme = CurrentSchemes.FirstOrDefault();
        }

        SelectedProfile.UpdatedAt = DateTime.Now;

        _logger.Info($"Usunięto schemat: {scheme.Name}");
    }

    private void AddStep()
    {
        if (SelectedMacro is null)
        {
            _logger.Warning("Najpierw wybierz makro.");
            return;
        }

        var step = CreateDefaultStep(SelectedMacro.Steps.Count + 1);

        SelectedMacro.Steps.Add(step);
        SelectedStep = step;

        _logger.Info($"Dodano krok: {step.Name}");
    }

    private void DeleteStep(MacroStep? stepToDelete = null)
    {
        if (SelectedMacro is null)
        {
            _logger.Warning("Nie wybrano makra.");
            return;
        }

        var step = stepToDelete ?? SelectedStep;

        if (step is null)
        {
            _logger.Warning("Nie wybrano kroku do usunięcia.");
            return;
        }

        SelectedMacro.Steps.Remove(step);
        ReorderSteps();

        if (SelectedStep == step)
        {
            SelectedStep = SelectedMacro.Steps.FirstOrDefault();
        }

        _logger.Info($"Usunięto krok: {step.Name}");
    }

    private void MoveStepUp()
    {
        if (SelectedMacro is null || SelectedStep is null)
        {
            return;
        }

        var index = SelectedMacro.Steps.IndexOf(SelectedStep);

        if (index <= 0)
        {
            return;
        }

        SelectedMacro.Steps.Move(index, index - 1);
        ReorderSteps();
    }

    private void MoveStepDown()
    {
        if (SelectedMacro is null || SelectedStep is null)
        {
            return;
        }

        var index = SelectedMacro.Steps.IndexOf(SelectedStep);

        if (index < 0 || index >= SelectedMacro.Steps.Count - 1)
        {
            return;
        }

        SelectedMacro.Steps.Move(index, index + 1);
        ReorderSteps();
    }

    private MacroStep CreateDefaultStep(int order)
    {
        return new MacroStep
        {
            Order = order,
            Name = "Delay 1000 ms",
            Kind = StepKind.Action,
            Action = new ActionDefinition
            {
                Kind = ActionKind.Delay,
                Parameters =
                {
                    ["Milliseconds"] = "1000"
                }
            }
        };
    }

    private void ReorderSteps()
    {
        if (SelectedMacro is null)
        {
            return;
        }

        for (var i = 0; i < SelectedMacro.Steps.Count; i++)
        {
            SelectedMacro.Steps[i].Order = i + 1;
        }
    }

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
