using blendporter.definition;
using blendporter.registry;
using Godot;
using System.Diagnostics;

namespace blendporter.dispatcher;

public static class PluginLogger
{
    public static void Log(LogLevel level, string msg)
    {
        var caller = new StackFrame(1).GetMethod()?.DeclaringType?.Name ?? "Unknown";
        var logLevel = (LogLevel)(SettingService.GetSetting(Settings.NameDictionary[Settings.LogLevelSetting])?.AsInt32()
                                  ?? (int)LogLevel.Unknown); 
        if (logLevel < level)
            return;
        var logString = $"{level} - [{caller}]: {msg}";
        switch (level)
        {
            case LogLevel.Error:
                GD.PushError(logString);
                return;
            case LogLevel.Warning:
                GD.PushWarning(logString);
                return;
            case LogLevel.Info:
            case LogLevel.Debug:
                GD.Print(logString);
                return;
            case LogLevel.Unknown:
            default:
                return;
        }
    }
}