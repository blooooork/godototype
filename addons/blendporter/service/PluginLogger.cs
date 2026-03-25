using blendporter.definition;
using blendporter.registry;
using Godot;
using System.Diagnostics;

namespace blendporter.dispatcher;

public class PluginLogger
{
    public static void Log(LogLevel level, string msg)
    {
        var caller = new StackFrame(1).GetMethod()?.DeclaringType?.Name ?? "Unknown";
        var logLevel = GetLogLevel();
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
    
    private static LogLevel GetLogLevel()
    {
        var logLevel = SettingService.GetSetting(Settings.NameDictionary[Settings.LogLevelSetting]);
        if (logLevel == null)
            return LogLevel.Unknown;
        return (LogLevel)logLevel.Value.AsInt32();
    }
    
    // TODO This should be a part of SettingRegistry as a whole
    public static void SetLogLevel(LogLevel logLevel)
    {
        SettingService.Register(Settings.NameDictionary[Settings.LogLevelSetting]);
        ProjectSettings.SetSetting(Settings.LogLevelSetting, (int)logLevel);
    }
}