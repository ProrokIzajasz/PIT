using PIT.Core.Automation;

namespace PIT.Core.Execution;

public interface IActionHandler
{
    bool CanHandle(ActionKind kind);

    Task ExecuteAsync(
        ActionDefinition action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default);
}