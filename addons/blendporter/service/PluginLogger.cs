using blendporter.definition;
using Godot;
using System.Diagnostics;
using blendporter.service.component;

namespace blendporter.service;

public static class PluginLogger
{
    public static void Log(LogLevel level, string msg)
    {
        var caller = new StackFrame(1).GetMethod()?.DeclaringType?.Name ?? "Unknown";
        var logLevel = (LogLevel)(PersistentSettingRegistry.GetSetting(Settings.LogLevelDefinition)?.AsInt32()
                                  ?? (int)Settings.DefaultLogLevel); 
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
            default:
                return;
        }
    }
}