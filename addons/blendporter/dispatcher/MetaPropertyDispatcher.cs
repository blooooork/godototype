using Godot;
using System.Collections.Generic;
using System.Linq;
using blendporter.definition;

namespace blendporter.dispatcher;

public class MetaPropertyDispatcher: IDispatcher
{

    public bool Dispatch(Node node)
    {
        // Validate node has MetaList
        var metaList = node.GetMetaList();
        if (metaList == null || metaList.Count == 0)
        {
            GD.Print($"{node.Name} had an empty meta list");
            return false;
        }
        // Validate node type is defined
        if (!DefinitionRegistry.IsTypeDefined(node.GetType()))
        {
            GD.Print($"{node.Name}'s type definition is not defined");
            return false;
        }
        // Attempt to apply each found dictionary
        return metaList.Select(meta => ApplyPropertyDictionary(node, meta))
            // Collect the outputs to a list and see if any were true
            .ToList()
            .Any(x => x);
    }

    private static bool ApplyPropertyDictionary(Node node, StringName dictionaryName)
    {
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
        GD.Print($"Metadata applied for \"[{node.GetType().Name}] {node.Name}\":");
        foreach (var key in appliedProperties.Keys)
            GD.Print($"{key}: {appliedProperties[key]}");
        return true;
    }

    public void Reset()
    {
        // Nothing to reset in this one
    }
}
