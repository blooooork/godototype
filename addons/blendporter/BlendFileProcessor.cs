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
        _metaDispatcher.Dispatch(scene);
        _sceneDispatcher.Dispatch(scene);
        _processDispatchers.ForEach(d => d.Reset());
    }
}