namespace blendporter.definition;

public static class Defaults
{
    public static readonly float[] Transform = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0];
    public static readonly float[] Position = [0, 0, 0];
    public const LogLevel LogLevel = definition.LogLevel.Warning;
    public const string LogLevelString = "Error,Warning,Info,Debug";
}