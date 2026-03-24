using System.Collections.Generic;
using blendporter.definition;
using Godot;
using blendporter.dispatcher;

namespace blendporter;

public partial class BlendFileProcessor : EditorScenePostImportPlugin
{
    private static readonly LogDispatcher LogDispatcher = new();
    private static readonly MetaPropertyDispatcher MetaDispatcher = new ();
    private static readonly SceneDispatcher SceneDispatcher = new ();
    private static readonly DirectoryDispatcher DirectoryDispatcher = new ();
    private static readonly List<IDispatcher> ProcessDispatchers = [MetaDispatcher, SceneDispatcher, DirectoryDispatcher];
    
    public override void _PostProcess(Node scene)
    {
        LogDispatcher.Dispatch((LogLevel.Info, $"Beginning post processing of scene {scene.Name}"));
        MetaDispatcher.Dispatch(scene);
        if (SceneDispatcher.IsSceneCreationEnabled())
            SceneDispatcher.Dispatch(scene);
        ProcessDispatchers.ForEach(d => d.Reset());
    }
}