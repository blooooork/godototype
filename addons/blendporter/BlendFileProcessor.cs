using blendporter.definition;
using Godot;
using blendporter.service;
using blendporter.service.component;

namespace blendporter;

public partial class BlendFileProcessor : EditorScenePostImportPlugin
{
    private string _currentBlendPath = "";

    public override void _PostProcess(Node scene)
    {
        PluginLogger.Log(LogLevel.Info, $"Beginning post processing of scene {scene.Name}");
        MetaPropertyService.ApplyProperties(scene);
        if (!string.IsNullOrEmpty(_currentBlendPath))
        {
            var nodePropertyMap = NodePropertyScanner.ScanBlend(_currentBlendPath);
            DynamicPropertyApplicator.Apply(scene, s => GetOptionValue(s), nodePropertyMap);
        }
        SceneService.CreateScenes(scene, GetOptionValue(Settings.CreateScenesSetting).AsBool());
        DirectoryService.Reset();
        SceneService.Reset();
    }

    public override void _GetImportOptions(string path)
    {
        if (!path.GetExtension().Equals("blend"))
            return;

        _currentBlendPath = path;
        ImportFileLocator.Add(path);

        foreach (var setting in Settings.FileSettings)
            AddImportOptionAdvanced(
                setting.PropertyType,
                setting.Name.ToString(),
                setting.SettingValue,
                setting.Hint,
                setting.HintString
            );

        var nodePropertyMap = NodePropertyScanner.ScanBlend(path);
        foreach (var (nodeName, props) in nodePropertyMap)
            foreach (var prop in props)
                AddImportOptionAdvanced(
                    Variant.Type.Int,
                    $"blendporter/{nodeName} [Custom Properties]/{prop.Name}",
                    0,
                    PropertyHint.Enum,
                    NodePropertyAggregator.BuildEnumString(prop)
                );
    }
}