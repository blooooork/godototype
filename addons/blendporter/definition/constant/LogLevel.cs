using System;
using Godot;

namespace blendporter.definition;

public enum LogLevel
{
    Error = 0,
    Warning = 1,
    Info = 2,
    Debug = 3,
}

public static LogLevel GetLogLevel()
{
    var value = ProjectSettings.GetSetting(Names.LogLevelSetting, (int)LogLevel.Warning).AsInt32();
    
    if (!Enum.IsDefined(typeof(LogLevel), value))
        return LogLevel.Warning;
    
    return (LogLevel)value;
}