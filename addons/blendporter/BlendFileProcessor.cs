using System.Linq;
using System.Collections.Generic;
using Godot;
using blendporter.dispatcher;

namespace blendporter;

public partial class BlendFileProcessor : EditorScenePostImportPlugin
{
    private readonly MetaPropertyDispatcher _metaDispatcher = new ();
    private readonly SceneDispatcher _sceneDispatcher = new ();
    private readonly DirectoryDispatcher _directoryDispatcher = new ();
    private readonly List<IDispatcher> _processDispatchers;

    public BlendFileProcessor()
    {
        _processDispatchers = [_metaDispatcher, _sceneDispatcher, _directoryDispatcher];
    }
    
    public override void _PostProcess(Node scene)
    {
        GD.Print($"Beginning post processing of scene {scene.Name}");
        var nodesAffected = ProcessNode(scene);
        var filesCreated = OutputSceneFiles(scene);
        GD.Print($"Scene post processing complete; {nodesAffected} nodes affected; {filesCreated} files created");
        _processDispatchers.ForEach(d => d.Reset());
    }

    private int ProcessNode(Node node)
    {
        // Recursively process all child nodes
        var children = new Godot.Collections.Array<Node>();
        foreach (var child in node.GetChildren())
            children.Add(child);

        var successCount = children.Sum(ProcessNode);
        // Attempt to apply meta properties
        if (_metaDispatcher.Dispatch(node))
            successCount++;
        return successCount;
    }

    private int OutputSceneFiles(Node node)
    {
        var children = new Godot.Collections.Array<Node>();
        foreach (var child in node.GetChildren())
        {
            var clonedChild = child.Duplicate();
            children.Add(clonedChild);
        }
        if (children.Count == 0)
            return 0;
        // Attempt to create output directory
        if (!_directoryDispatcher.Dispatch(node))
        {
            GD.PushError($"Output directory for scene \"{node.Name}\" couldn't be created");
            return 0;
        }
        // Output each child of main node as a scene file
        _sceneDispatcher.OutputPath = DirectoryDispatcher.OutputPath;
        var successCount = 0;
        successCount += children.Count(_sceneDispatcher.Dispatch);
        return successCount;
    }
}