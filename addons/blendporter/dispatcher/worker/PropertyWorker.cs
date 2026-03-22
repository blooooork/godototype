using Godot;
using blendporter.definition;
using System.Collections.Generic;
using System.Linq;

namespace blendporter.dispatcher.worker;

public class PropertyWorker : IWorker
{
    #nullable enable
    public bool Work(Node node, object? details)
    {
        if (details is not StringName dictionaryName)
            return false;
        // Validate dictionary is expected structure for custom properties
        var propDictionary = DefinitionRegistry.ValidatePropertyDictionary(node, dictionaryName);
        if (propDictionary == null || propDictionary.Count == 0)
        {
            GD.PushWarning($"{node.Name} did not have a valid property dictionary");
            return false;
        }
        GD.Print($"[PropertyWorker] \"{node.Name}\" extras keys: [{string.Join(", ", propDictionary.Keys)}]");
        // Verify type is defined
        var type = node.GetType();
        var appliedProperties = new Dictionary<string, object>();
        
        // TODO Need to change this to be getting the PropertyDefinition based off the CustomName
        //          Property Definition in itself needs to have its Type that it casts the node to in Applicator
        //          In here it gets the definition by matching CustomName and then verifies node is matching type
        //              Make sure we do subtype matching or whatever to ensure we match down to Node3d (or even RefCounted)
        //          Then converts the given value with the converter
        //          Then applies converted value with the applicator
        foreach (var kV in propDictionary)
        {
            var propDef = PropertyDefinitions.GetPropertyDefinition((string)kV.Key);
            if (propDef == null)
            {
                GD.Print($"[PropertyWorker] No definition found for key: \"{kV.Key}\"");
                continue;
            }
            GD.Print($"[PropertyWorker] Found definition \"{propDef.Name}\" for key \"{kV.Key}\", node type: {node.GetType().Name}, def type: {propDef.Type.Name}");
            var propValue = propDef.Convert(kV.Value);
            if (propValue == null)
            {
                GD.Print($"[PropertyWorker] Convert returned null for key: \"{kV.Key}\", raw value: {kV.Value}");
                continue;
            }
            GD.Print($"[PropertyWorker] Converted \"{kV.Key}\" to: {propValue}");
            propDef.Apply(node, propValue);
            appliedProperties.Add(kV.Key.ToString(), propValue);
        }
        
        // // Get the applicator and definition for data type
        // var typeDefinition = DefinitionRegistry.GetTypeDefinitions(type);
        // if (typeDefinition == null || typeDefinition.Count == 0)
        // {
        //     GD.PushWarning($"{node.Name}'s type definition is not defined or empty");
        //     return false;
        // }
        // // TODO Right here is the inefficiency
        // //          Looping here
        // // Match incoming properties to their definitions
        // var matched = propDictionary
        //     .Select(kv => new MatchedProperty(kv, typeDefinition.GetValueOrDefault(kv.Key.ToString())))
        //     .Where(m => m.Definition != null);
        // // TODO Then looping here again
        // //          Solution is to just do all the actions in the first loop above and only use GetPropertyDefinition
        // //              Don't bother with MatchedProperty shit
        // // Convert property to configured type and apply to node
        // foreach (var match in matched)
        // {
        //     var propName =  match.Entry.Key.ToString();
        //     var propValue = match.Entry.Value;
        //     var convertedValue = match.Definition.Convert(propValue);
        //     typeDefinition[propName].Apply(node, convertedValue);
        //     appliedProperties.Add(propName, convertedValue);
        // }
        // Log applied properties if there were any
        if (appliedProperties.Count == 0)
            return false;
        var props = string.Join(", ", appliedProperties.Select(kv => $"\"{kv.Key}\": {kv.Value}"));
        GD.Print($"Metadata applied for \"[{node.GetType().Name}] {node.Name}\": {props}");
        return true;
    }
    #nullable disable
}