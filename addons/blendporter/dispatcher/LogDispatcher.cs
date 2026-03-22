using blendporter.definition;

namespace blendporter.dispatcher;

public class LogDispatcher : IDispatcher
{
    private static LogLevel _logLevel { get; set; } = LogLevel.Info;

    public bool Dispatch(object incomingObject)
    {
        // TODO Implement
        return false;
    }

    public void Reset()
    {
        // Nothing to do here
    }
}