using blendporter.definition;
using Godot;

namespace blendporter.service;

public static class DirectoryService
{
    private const string BasePath = "res://blendporter";
    public static string OutputPath { get; set; }
    
    public static bool CreateDirectory(Node node)
    {
        OutputPath = $"{BasePath}/{node.Name}/";
        var error = DirAccess.MakeDirRecursiveAbsolute(OutputPath);
        if (error == Error.Ok)
            return true;
        PluginLogger.Log(LogLevel.Error,
            $"Directory could not be created. Failed with error \"{error.ToString()}\"");
        return false;
    }

    public static void Reset()
    {
        OutputPath = null;
    }
}