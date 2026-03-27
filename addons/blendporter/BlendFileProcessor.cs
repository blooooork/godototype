using blendporter.definition;
using Godot;
using blendporter.service;
using blendporter.service.component;
using System.Linq;

namespace blendporter;

public partial class BlendFileProcessor : EditorScenePostImportPlugin
{
    public override void _PostProcess(Node scene)
    {
        PluginLogger.Log(LogLevel.Info, $"Beginning post processing of scene {scene.Name}");
        MetaPropertyService.ApplyProperties(scene);
        SceneService.CreateScenes(scene, GetOptionValue(Settings.CreateScenesSetting).AsBool());
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

        var nodePropertyMap = NodePropertyScanner.ScanBlend(path);
        foreach (var (nodeName, props) in nodePropertyMap)
            foreach (var prop in props)
                AddImportOptionAdvanced(
                    Variant.Type.Int,
                    $"blendporter/{nodeName} [Custom Properties]/{prop.Name}",
                    0,
                    PropertyHint.Enum,
                    BuildEnumString(prop)
                );
    }

    private static string BuildEnumString(NodeProperty prop)
    {
        const string none = "None";

        var key = NodePropertyAggregator.GetPropertyKey(prop.VariantType, prop.ArrayLength);
        if (key == null)
            return none;

        var settable = NodePropertyAggregator.GetSettableProperties(prop.NodeType);
        if (!settable.TryGetValue(key, out var matches) || matches.Count == 0)
            return none;

        return none + "," + string.Join(",", matches.Select(p => p.Name));
    }
}