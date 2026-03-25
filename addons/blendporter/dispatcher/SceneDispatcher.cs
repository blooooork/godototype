using System.Linq;
using blendporter.definition;
using blendporter.dispatcher.worker;
using blendporter.registry;
using Godot;
using Godot.Collections;

namespace blendporter.dispatcher;

public static class SceneDispatcher
{
    private static string _outputPath;

    public static void CreateScenes(Node node)
    {
        var children = new Array<Node>();
        foreach (var child in node.GetChildren())
        {
            var clonedChild = (Node3D)child.Duplicate();
            children.Add(clonedChild);
        }
        if (children.Count == 0)
            return;
        // Attempt to create output directory
        if (!DirectoryDispatcher.CreateDirectory(node))
        {
            var errorString = $"Output directory for scene \"{node.Name}\" couldn't be created";
            PluginLogger.Log(LogLevel.Error, errorString);
            return;
        }
        // Output main node and children as scene files
        _outputPath = DirectoryDispatcher.OutputPath;
        var successCount = FileWorker.CreateSceneFiles(node, _outputPath) ? 1 : 0;
        successCount += children.Count(c => FileWorker.CreateSceneFiles(c, _outputPath));
        var successString = $"{successCount} files created from node \"{node.Name}\"";
        PluginLogger.Log(LogLevel.Info, $"{successString}");
    }

    public static void Reset()
    {
        _outputPath = null;
    }

    // TODO This should be something in SettingRegistry
    public static bool IsSceneCreationEnabled()
    {
        var creationEnabled = SettingRegistry.GetSetting(Settings.NameDictionary[Settings.CreateScenesSetting]);
        return creationEnabled != null && creationEnabled.Value.AsBool();
    }
}