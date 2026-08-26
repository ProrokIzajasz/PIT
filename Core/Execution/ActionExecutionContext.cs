using PIT.Core.Logging;

namespace PIT.Core.Execution;

public sealed class ActionExecutionContext
{
    public IPitLogger Logger { get; }

    public ActionExecutionContext(IPitLogger logger)
    {
        Logger = logger;
    }
}