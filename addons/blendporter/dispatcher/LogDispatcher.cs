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
        SetLogLevel(Defaults.LogLevel);
    }
    
    #nullable enable
    public static LogLevel? GetLogLevel()
    {
        SettingRegistry.Register(SettingRegistry.SettingDefinitions[Names.LogLevelSetting]);
        var value = ProjectSettings.GetSetting(Names.LogLevelSetting);
        return value.Obj is null ? null : (LogLevel)value.AsInt32();
    }
    #nullable disable

    public static void SetLogLevel(LogLevel logLevel)
    {
        SettingRegistry.Register(SettingRegistry.SettingDefinitions[Names.LogLevelSetting]);
        ProjectSettings.SetSetting(Names.LogLevelSetting, (int)logLevel);
    }
}