using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using System.Linq;

namespace blendporter.definition;

public static class DefinitionRegistry
{
    private static readonly System.Collections.Generic.Dictionary<Type, List<PropertyDefinition>> Registry = new()
    {
        [typeof(Node3D)] = [
            PropertyDefinitions.PositionDefinition,
            PropertyDefinitions.TransformDefinition
        ],
        [typeof(RigidBody3D)] = [
            PropertyDefinitions.GravityScaleDefinition, 
            PropertyDefinitions.MassDefinition,
            PropertyDefinitions.TransformDefinition,
            PropertyDefinitions.PositionDefinition
        ],
        [typeof(MeshInstance3D)] = [
            PropertyDefinitions.TransformDefinition,
            PropertyDefinitions.PositionDefinition
        ]
    };

    public static bool IsTypeDefined(Type type)
    {
        return Registry.ContainsKey(type);
    }

    public static Dictionary ValidatePropertyDictionary(Node node, StringName name)
    {
        if (!DictionaryNames.All.Contains(name))
            return [];
        var metaValue = node.GetMeta(name);
        return metaValue.Obj as Dictionary ?? [];
    }
    
    #nullable enable
    
    public static System.Collections.Generic.Dictionary<string, PropertyDefinition>? GetTypeDefinitions(Type type)
    {
        return IsTypeDefined(type) ? Registry[type].ToDictionary(d => d.Name, d => d) : null;
    }
}