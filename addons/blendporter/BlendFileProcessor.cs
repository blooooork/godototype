using blendporter.definition;
using Godot;
using blendporter.dispatcher;
using blendporter.dispatcher.worker;

namespace blendporter;

public partial class BlendFileProcessor : EditorScenePostImportPlugin
{
    public override void _PostProcess(Node scene)
    {
        PluginLogger.Log(LogLevel.Info, $"Beginning post processing of scene {scene.Name}");
        MetaPropertyService.ApplyProperties(scene);
        SceneService.CreateScenes(scene);
        DirectoryService.Reset();
        SceneService.Reset();
    }

    public override void _GetImportOptions(string path)
    {
        if (!path.GetExtension().Equals("blend"))
            return;

        ImportFileLocator.Add(path);
        foreach (var setting in Settings.FileSettings)
            AddImportOptionAdvanced(
                setting.PropertyType,
                setting.Name.ToString(),
                setting.SettingValue,
                setting.Hint,
                setting.HintString
            );
    }
}