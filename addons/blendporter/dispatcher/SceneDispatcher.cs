using System.Linq;
using blendporter.dispatcher.worker;
using Godot;
using Godot.Collections;

namespace blendporter.dispatcher;

public class SceneDispatcher : IDispatcher
{
    private string _outputPath;
    private static readonly DirectoryDispatcher DirectoryDispatcher = new();
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
            GD.PrintErr($"Output directory for scene \"{node.Name}\" couldn't be created");
            return false;
        }
        // Output main node and children as scene files
        _outputPath = DirectoryDispatcher.OutputPath;
        var successCount = FileWorker.Work(node, _outputPath) ? 1 : 0;
        successCount += children.Count(c => FileWorker.Work(c, _outputPath));
        GD.Print($"{successCount} files created from node \"{node.Name}\"");
        return successCount > 0;
    }

    public void Reset()
    {
        _outputPath = null;
    }
}