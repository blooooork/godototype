using System;
using blendporter.definition;
using blendporter.dispatcher.worker;
using System.Diagnostics;

namespace blendporter.dispatcher;

public class LogDispatcher : IDispatcher
{
    private static readonly LogWorker LogWorker = new();
    public bool Dispatch(object incomingObject)
    {
        var caller = new StackFrame(1).GetMethod()?.DeclaringType?.Name ?? "Unknown";
        return incomingObject is ValueTuple<LogLevel, string> logEntry && 
            LogWorker.Work(logEntry.Item1, (caller, logEntry.Item2));
    }

    public void Reset()
    {
        LogWorker.SetLogLevel(Settings.DefaultLogLevel);
    }
    

}