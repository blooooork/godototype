using blendporter.definition;
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace blendporter.service.component;

public record NodeProperty(string Name, Godot.Variant.Type VariantType, System.Type NodeType, int? ArrayLength = null);

public static class NodePropertyScanner
{
    public static Dictionary<string, List<NodeProperty>> ScanBlend(string blendPath)
    {
        var result = new Dictionary<string, List<NodeProperty>>();

        var config = new ConfigFile();
        if (config.Load(blendPath + ".import") != Error.Ok)
            return result;
        if (!config.HasSectionKey("remap", "path"))
            return result;

        var compiledPath = config.GetValue("remap", "path").AsString();
        if (string.IsNullOrEmpty(compiledPath))
            return result;

        var packed = ResourceLoader.Load<PackedScene>(compiledPath);
        if (packed == null)
            return result;

        var instance = packed.Instantiate();
        WalkNode(instance, result);
        instance.Free();

        PluginLogger.Log(LogLevel.Debug, $"Scanned {result.Count} nodes with custom properties in {blendPath.GetFile()}");
        return result;
    }

    private static void WalkNode(Node node, Dictionary<string, List<NodeProperty>> result)
    {
        if (node.HasMeta("extras"))
        {
            var extras = node.GetMeta("extras").AsGodotDictionary();
            var nodeType = node.GetType();
            var props = extras
                .Where(kv => !string.IsNullOrEmpty(kv.Key.AsString()))
                .Select(kv =>
                {
                    int? length = kv.Value.VariantType == Variant.Type.PackedFloat32Array
                        ? kv.Value.As<float[]>().Length
                        : null;
                    return new NodeProperty(kv.Key.AsString(), kv.Value.VariantType, nodeType, length);
                })
                .ToList();

            if (props.Count > 0)
            {
                var name = node.Name.ToString();
                result[name] = result.TryGetValue(name, out var existing)
                    ? existing.UnionBy(props, p => p.Name).ToList()
                    : props;
            }
        }

        foreach (var child in node.GetChildren())
            WalkNode(child, result);
    }
}
