using System.Linq;
using blendporter.definition;
using blendporter.service.component;
using Godot;
using Godot.Collections;

namespace blendporter.service;

public static class SceneService
{
    private static string _outputPath;

    public static void CreateScenes(Node scene, bool shouldCreateScenes)
    {
        if (shouldCreateScenes)
            CreateNodeScene(scene);
    }
    
    private static void CreateNodeScene(Node node)
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
        if (!DirectoryService.CreateDirectory(node))
        {
            var errorString = $"Output directory for scene \"{node.Name}\" couldn't be created";
            PluginLogger.Log(LogLevel.Error, errorString);
            return;
        }
        // Output main node and children as scene files
        _outputPath = DirectoryService.OutputPath;
        var successCount = SceneCreator.Create(node, _outputPath) ? 1 : 0;
        successCount += children.Count(c => SceneCreator.Create(c, _outputPath));
        var successString = $"{successCount} files created from node \"{node.Name}\"";
        PluginLogger.Log(LogLevel.Info, $"{successString}");
    }

    public static void Reset()
    {
        _outputPath = null;
    }
}