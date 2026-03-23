using Godot;
using blendporter.definition;
using System.Collections.Generic;
using System.Linq;

namespace blendporter.dispatcher.worker;

public class PropertyWorker : IWorker
{
    private static readonly ValidationWorker ValidationWorker = new();

    #nullable enable
    public bool Work(object incomingObject, object? details)
    {
        if (incomingObject is not Node3D node)
            return false;
        if (details is not StringName dictionaryName)
            return false;
        // Validate dictionary is expected structure for custom properties
        if (!ValidationWorker.Work(dictionaryName, ValidationWorker.Type.DictionaryName))
            return false;
        var propDictionary = (Godot.Collections.Dictionary)Converters.DictionaryConverter.Invoke(node.GetMeta(dictionaryName));
        if (propDictionary == null || propDictionary.Count == 0)
        {
            GD.PushWarning($"{node.Name} did not have a valid property dictionary");
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
        GD.Print($"Metadata applied for \"[{node.GetType().Name}] {node.Name}\": {props}");
        return true;
    }
    #nullable disable
}