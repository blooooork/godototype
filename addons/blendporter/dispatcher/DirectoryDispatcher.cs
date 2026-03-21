using Godot;

namespace blendporter.dispatcher;

public class DirectoryDispatcher : IDispatcher
{
    private const string BasePath = "res://blendporter";
    public static string OutputPath { get; set; }
    
    public bool Dispatch(Node node)
    {
        OutputPath = $"{BasePath}/{node.Name}/";
        var error = DirAccess.MakeDirRecursiveAbsolute(OutputPath);
        if (error == Error.Ok)
            return true;
        GD.PrintErr($"Directory could not be created. Failed with error \"{error.ToString()}\"");
        return false;
    }

    public void Reset()
    {
        OutputPath = null;
    }
}