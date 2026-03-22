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
        // Verify type is defined
        var type = node.GetType();
        var appliedProperties = new Dictionary<string, object>();
        // Get the applicator and definition for data type
        var typeDefinition = DefinitionRegistry.GetTypeDefinitions(type);
        if (typeDefinition == null || typeDefinition.Count == 0)
        {
            GD.PushWarning($"{node.Name}'s type definition is not defined or empty");
            return false;
        }
        // Match incoming properties to their definitions
        var matched = propDictionary
            .Select(kv => new MatchedProperty(kv, typeDefinition.GetValueOrDefault(kv.Key.ToString())))
            .Where(m => m.Definition != null);
        // Convert property to configured type and apply to node
        foreach (var match in matched)
        {
            var propName =  match.Entry.Key.ToString();
            var propValue = match.Entry.Value;
            var convertedValue = match.Definition.ConvertValue(propValue);
            typeDefinition[propName].ApplyProperty(node, convertedValue);
            appliedProperties.Add(propName, convertedValue);
        }
        // Log applied properties if there were any
        if (appliedProperties.Count == 0)
            return false;
        var props = string.Join(", ", appliedProperties.Select(kv => $"\"{kv.Key}\": {kv.Value}"));
        GD.Print($"Metadata applied for \"[{node.GetType().Name}] {node.Name}\": {props}");
        return true;
    }
    #nullable disable
}