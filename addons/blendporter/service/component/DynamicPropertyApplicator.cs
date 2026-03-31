#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using blendporter.definition;

namespace blendporter.service.component;

public static class DynamicPropertyApplicator
{
    public static void Apply(Node scene, Func<string, Variant> getOption, Dictionary<string, List<NodeProperty>> nodePropertyMap)
    {
        foreach (var (nodeName, props) in nodePropertyMap)
        {
            var node = FindNode(scene, nodeName);
            if (node == null || !node.HasMeta("extras")) continue;

            var extras = node.GetMeta("extras").AsGodotDictionary();

            foreach (var prop in props)
            {
                var optionPath = $"blendporter/{nodeName} [Custom Properties]/{prop.Name}";
                var selectedIndex = getOption(optionPath).AsInt32();
                if (selectedIndex == 0) continue;

                var enumOptions = NodePropertyAggregator.BuildEnumString(prop).Split(',');
                if (selectedIndex >= enumOptions.Length) continue;
                var targetPropName = enumOptions[selectedIndex];

                Variant blenderValue = default;
                foreach (var kv in extras)
                {
                    if (kv.Key.AsString() != prop.Name) continue;
                    blenderValue = kv.Value;
                    break;
                }
                if (blenderValue.VariantType == Variant.Type.Nil) continue;

                var propInfo = prop.NodeType.GetProperty(targetPropName, BindingFlags.Public | BindingFlags.Instance);
                if (propInfo == null || !propInfo.CanWrite) continue;

                var converted = NodePropertyAggregator.ConvertVariant(blenderValue, propInfo.PropertyType);
                if (converted == null) continue;

                propInfo.SetValue(node, converted);
                PluginLogger.Log(LogLevel.Debug, $"Dynamic apply: [{prop.NodeType.Name}] {node.Name}.{targetPropName} = {converted}");
            }
        }
    }

    private static Node? FindNode(Node root, string name)
    {
        if (root.Name == name) return root;
        return root.FindChild(name, true, false);
    }
}
