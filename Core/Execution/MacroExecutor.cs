using PIT.Core.Automation;
using PIT.Core.Logging;

namespace PIT.Core.Execution;

public sealed class MacroExecutor
{
    private readonly IPitLogger _logger;
    private readonly ActionDispatcher _actionDispatcher;

    public ExecutionState State { get; private set; } = ExecutionState.Idle;

    public MacroExecutor(IPitLogger logger, ActionDispatcher actionDispatcher)
    {
        _logger = logger;
        _actionDispatcher = actionDispatcher;
    }

    public async Task ExecuteAsync(MacroDefinition macro, CancellationToken cancellationToken = default)
    {
        if (!macro.IsEnabled)
        {
            _logger.Warning($"Makro '{macro.Name}' jest wyłączone.");
            return;
        }

        State = ExecutionState.Running;
        _logger.Info($"Start makra: {macro.Name}");

        try
        {
            var steps = macro.Steps
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.Order)
                .ToList();

            foreach (var step in steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.Info($"Krok: {step.Name}");

                if (step.Kind == StepKind.Action && step.Action is not null)
                {
                    await _actionDispatcher.ExecuteAsync(step.Action, cancellationToken);
                }
                else if (step.Kind == StepKind.Condition && step.Condition is not null)
                {
                    _logger.Warning($"Warunek '{step.Condition.Kind}' dodamy w module detektorów.");
                }
                else if (step.Kind == StepKind.MacroReference)
                {
                    _logger.Warning("Referencje do innych makr dodamy po zbudowaniu MacroRegistry.");
                }
                else
                {
                    _logger.Warning($"Krok '{step.Name}' nie ma poprawnej konfiguracji.");
                }
            }

            State = ExecutionState.Completed;
            _logger.Info($"Zakończono makro: {macro.Name}");
        }
        catch (OperationCanceledException)
        {
            State = ExecutionState.Stopped;
            _logger.Warning($"Zatrzymano makro: {macro.Name}");
        }
        catch (Exception ex)
        {
            State = ExecutionState.Failed;
            _logger.Error($"Błąd makra '{macro.Name}': {ex.Message}");
        }
    }
}