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
        var propDictionary = (Godot.Collections.Dictionary)ConverterDefinitions.DictionaryConverter.Invoke(node.GetMeta(dictionaryName));
        if (propDictionary == null || propDictionary.Count == 0)
        {
            GD.PushWarning($"{node.Name} did not have a valid property dictionary");
            return false;
        }
        var appliedProperties = new Dictionary<string, object>();
        
        // TODO Next should be changing PropertyDefinitions to DefinitionRegistry and deletion what is now DefinitionRegistry
        //          Think comment below has been addressed
        // TODO Need to change this to be getting the PropertyDefinition based off the CustomName
        //          Property Definition in itself needs to have its Type that it casts the node to in Applicator
        //          In here it gets the definition by matching CustomName and then verifies node is matching type
        //              Make sure we do subtype matching or whatever to ensure we match down to Node3d (or even RefCounted)
        //          Then converts the given value with the converter
        //          Then applies converted value with the applicator
        foreach (var kV in propDictionary)
        {
            var propDef = DefinitionRegistry.GetPropertyDefinition((string)kV.Key);
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