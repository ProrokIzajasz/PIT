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
}
