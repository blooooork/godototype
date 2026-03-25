using blendporter.definition;
using Godot;
using blendporter.dispatcher;

namespace blendporter;

public partial class BlendFileProcessor : EditorScenePostImportPlugin
{
    public override void _PostProcess(Node scene)
    {
        PluginLogger.Log(LogLevel.Info, $"Beginning post processing of scene {scene.Name}");
        MetaPropertyService.ApplyProperties(scene);
        if (SceneService.IsSceneCreationEnabled())
            SceneService.CreateScenes(scene);
        DirectoryService.Reset();
        SceneService.Reset();
    }
}