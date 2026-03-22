using System.Collections.Generic;
using System;
using Godot;
using System.Linq;

namespace blendporter.definition;

public static class PropertyDefinitions
{
    public static readonly PropertyDefinition GravityScaleDefinition = new (
        CustomNames.GravityScale,
        typeof(RigidBody3D),
        ConverterDefinitions.FloatConverter,
        ApplicatorDefinitions.GravityScaleApplicator
    );
    public static readonly PropertyDefinition MassDefinition = new (
        CustomNames.Mass, 
        typeof(RigidBody3D),
        ConverterDefinitions.FloatConverter, 
        ApplicatorDefinitions.MassApplicator
    );
    
    public static readonly PropertyDefinition TransformDefinition = new (
        CustomNames.Transform,
        typeof(Node3D),
        ConverterDefinitions.TransformConverter,
        ApplicatorDefinitions.TransformApplicator
    );

    public static readonly PropertyDefinition PositionDefinition = new (
        CustomNames.ResetPosition,
        typeof(Node3D),
        ConverterDefinitions.Vector3Converter,
        ApplicatorDefinitions.PositionApplicator
    );
    
    private static readonly PropertyDefinition[] PropertyList =
    [
        GravityScaleDefinition,
        MassDefinition,
        TransformDefinition,
        PositionDefinition
    ];

    public static readonly Dictionary<Type, List<PropertyDefinition>> All =
        PropertyList.GroupBy(d => d.Type)
            .ToDictionary(g => g.Key, g => g.ToList());

    #nullable enable
    public static PropertyDefinition? GetPropertyDefinition(string customName)
    {
        return PropertyList.FirstOrDefault(d => d.Name == customName);
    }
    #nullable disable
}