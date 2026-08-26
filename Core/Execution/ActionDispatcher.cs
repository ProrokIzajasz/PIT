using PIT.Core.Automation;
using PIT.Core.Logging;

namespace PIT.Core.Execution;

public sealed class ActionDispatcher
{
    private readonly IReadOnlyList<IActionHandler> _handlers;
    private readonly IPitLogger _logger;

    public ActionDispatcher(IEnumerable<IActionHandler> handlers, IPitLogger logger)
    {
        _handlers = handlers.ToList();
        _logger = logger;
    }

    public async Task ExecuteAsync(
        ActionDefinition action,
        CancellationToken cancellationToken = default)
    {
        var handler = _handlers.FirstOrDefault(x => x.CanHandle(action.Kind));

        if (handler is null)
        {
            _logger.Warning($"Brak handlera dla akcji: {action.Kind}");
            return;
        }

        var context = new ActionExecutionContext(_logger);

        await handler.ExecuteAsync(action, context, cancellationToken);
    }
}