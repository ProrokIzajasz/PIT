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

public sealed partial class MainViewModel : INotifyPropertyChanged
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
}
