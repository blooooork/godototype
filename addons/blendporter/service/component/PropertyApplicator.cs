using Godot;
using blendporter.definition;
using System.Collections.Generic;
using System.Linq;

namespace blendporter.service.component;

public static class PropertyApplicator
{
    public static bool Apply(Node node, StringName dictionaryName)
    {
        // Validate dictionary is expected structure for custom properties
        if (!ObjectValidator.Validate(dictionaryName, ObjectValidator.Type.DictionaryName))
            return false;
        var propDictionary = Converters.DictionaryConverter.Invoke(node.GetMeta(dictionaryName)) as Godot.Collections.Dictionary;
        if (propDictionary == null || propDictionary.Count == 0)
        {
            PluginLogger.Log(LogLevel.Warning, $"{node.Name} did not have a valid property dictionary");
            return false;
        }
        var appliedProperties = new Dictionary<string, object>();
        foreach (var kV in propDictionary)
        {
            var propDef = Properties.GetPropertyDefinition((string)kV.Key);
            if (propDef == null)
                continue;
            var propValue = propDef.Convert(kV.Value);
            if (propValue == null)
                continue;
            propDef.Apply(node, propValue);
            appliedProperties.Add(kV.Key.ToString(), propValue);
        }
        if (appliedProperties.Count == 0)
            return false;
        var props = string.Join(", ", appliedProperties.Select(kv => $"\"{kv.Key}\": {kv.Value}"));
        PluginLogger.Log(LogLevel.Debug, $"Metadata applied for \"[{node.GetType().Name}] {node.Name}\": {props}");
        return true;
    }
}