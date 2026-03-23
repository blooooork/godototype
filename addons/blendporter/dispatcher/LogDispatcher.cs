using Godot;
using System;
using blendporter.definition;
using blendporter.registry;

namespace blendporter.dispatcher;

public class LogDispatcher : IDispatcher
{
    private static readonly SettingRegistry SettingRegistry = new();
    public bool Dispatch(object incomingObject)
    {
        if (incomingObject is not Tuple<LogLevel, string> logEntry)
            return false;
        // TODO Implement
        return false;
    }

    public void Reset()
    {
        SetLogLevel(Settings.LogLevel);
    }
    
    #nullable enable
    public static LogLevel? GetLogLevel()
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