using blendporter.definition;
using Godot;

namespace blendporter.dispatcher;

public class DirectoryDispatcher : IDispatcher
{
    private const string BasePath = "res://blendporter";
    public static string OutputPath { get; set; }
    
    public bool Dispatch(object incomingObject)
    {
        if(incomingObject is not Node3D node)
            return false;
        OutputPath = $"{BasePath}/{node.Name}/";
        var error = DirAccess.MakeDirRecursiveAbsolute(OutputPath);
        if (error == Error.Ok)
            return true;
        PluginLogger.Log(LogLevel.Error,
            $"Directory could not be created. Failed with error \"{error.ToString()}\"");
        return false;
    }

    public void Reset()
    {
        OutputPath = null;
    }
}