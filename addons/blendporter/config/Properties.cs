using System.Collections.Generic;
using System;
using Godot;
using System.Linq;

namespace blendporter.definition;

public class Properties
{
    // Dictionary names
    public static readonly StringName BlenderMetaKey = new ("extras");
    public static readonly IReadOnlyList<StringName> MetaKeys = [BlenderMetaKey];
    // Property names
    public static readonly StringName Transform = new("transform");
    public static readonly StringName GravityScale = new("gravity_scale");
    public static readonly StringName Mass = new("mass");
    public static readonly StringName ResetPosition = new("reset_position");
    
    public static readonly float[] OriginTransform = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0];
    public static readonly float[] OriginPosition = [0, 0, 0];
    
    // Property Definitions
    private static readonly PropertyDefinition GravityScaleDefinition = new (
        GravityScale,
        typeof(RigidBody3D),
        Converters.FloatConverter,
        Applicators.GravityScaleApplicator
    );
    private static readonly PropertyDefinition MassDefinition = new (
        Mass, 
        typeof(RigidBody3D),
        Converters.FloatConverter, 
        Applicators.MassApplicator
    );
    
    private static readonly PropertyDefinition TransformDefinition = new (
        Transform,
        typeof(Node3D),
        Converters.TransformConverter,
        Applicators.TransformApplicator
    );

    private static readonly PropertyDefinition PositionDefinition = new (
        ResetPosition,
        typeof(Node3D),
        Converters.Vector3Converter,
        Applicators.PositionApplicator
    );
    
    private static readonly PropertyDefinition[] PropertyList =
    [
        GravityScaleDefinition,
        MassDefinition,
        TransformDefinition,
        PositionDefinition
    ];

    public static readonly Dictionary<Type, List<PropertyDefinition>> TypeDictionary =
        PropertyList.GroupBy(d => d.Type)
            .ToDictionary(g => g.Key, g => g.ToList());

#nullable enable
    public static PropertyDefinition? GetPropertyDefinition(string customName)
    {
        return PropertyList.FirstOrDefault(d => d.Name.ToString() == customName);
    }
#nullable disable
}