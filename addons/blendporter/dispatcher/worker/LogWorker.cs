using blendporter.definition;
using blendporter.registry;
using Godot;
using System;

namespace blendporter.dispatcher.worker;

public class LogWorker : IWorker
{
    #nullable enable
    
    public bool Work(object incomingObject, object? details)
    {
        if (incomingObject is not LogLevel incomingLevel || details is not ValueTuple<string, string> incomingTuple)
            return false;
        var incomingCaller =  incomingTuple.Item1;
        var incomingLog = incomingTuple.Item2;
        var logLevel = GetLogLevel();
        if (logLevel < incomingLevel)
            return false;
        var logString = $"{incomingLevel} - [{incomingCaller}]: {incomingLog}";
        switch (incomingLevel)
        {
            case LogLevel.Error:
                GD.PushError(logString);
                break;
            case LogLevel.Warning:
                GD.PushWarning(logString);
                break;
            case LogLevel.Info:
            case LogLevel.Debug:
                GD.Print(logString);
                break;
            default:
                return false;
        }
        return true;
    }
    
    private static LogLevel? GetLogLevel()
    {
        SettingRegistry.Register(Settings.NameDictionary[Settings.LogLevelSetting]);
        var value = ProjectSettings.GetSetting(Settings.LogLevelSetting);
        return value.Obj is null ? null : (LogLevel)value.AsInt32();
    }
    
    #nullable disable

    public static void SetLogLevel(LogLevel logLevel)
    {
        SettingRegistry.Register(Settings.NameDictionary[Settings.LogLevelSetting]);
        ProjectSettings.SetSetting(Settings.LogLevelSetting, (int)logLevel);
    }
    

}