using System.Linq;
using blendporter.definition;
using blendporter.dispatcher.worker;
using blendporter.registry;
using Godot;
using Godot.Collections;

namespace blendporter.dispatcher;

public class SceneDispatcher : IDispatcher
{
    private string _outputPath;
    private static readonly DirectoryDispatcher DirectoryDispatcher = new();
    private static readonly LogDispatcher LogDispatcher = new ();
    private static readonly FileWorker FileWorker = new();

    public bool Dispatch(object incomingObject)
    {
        if(incomingObject is not  Node3D node)
            return false;
        var children = new Array<Node>();
        foreach (var child in node.GetChildren())
        {
            var clonedChild = child.Duplicate();
            children.Add(clonedChild);
        }
        if (children.Count == 0)
            return false;
        // Attempt to create output directory
        if (!DirectoryDispatcher.Dispatch(node))
        {
            var errorString = $"Output directory for scene \"{node.Name}\" couldn't be created";
            LogDispatcher.Dispatch((LogLevel.Error, errorString));
            return false;
        }
        // Output main node and children as scene files
        _outputPath = DirectoryDispatcher.OutputPath;
        var successCount = FileWorker.Work(node, _outputPath) ? 1 : 0;
        successCount += children.Count(c => FileWorker.Work(c, _outputPath));
        var successString = $"{successCount} files created from node \"{node.Name}\"";
        LogDispatcher.Dispatch((LogLevel.Info, $"{successString}"));
        return successCount > 0;
    }

    public void Reset()
    {
        _outputPath = null;
    }

    public static bool IsSceneCreationEnabled()
    {
        var creationEnabled = SettingRegistry.GetSetting(Settings.NameDictionary[Settings.CreateScenesSetting]);
        if (creationEnabled == null)
            return false;
        return creationEnabled.Value.AsBool();
    }
}