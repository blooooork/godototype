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

    public static readonly float[] OriginTransform = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0];

    // Property Definitions — only special-case properties that require custom application logic.
    // Standard node properties are applied dynamically via DynamicPropertyApplicator.
    private static readonly PropertyDefinition TransformDefinition = new (
        Transform,
        typeof(Node3D),
        Converters.TransformConverter,
        Applicators.TransformApplicator
    );

    private static readonly PropertyDefinition[] PropertyList =
    [
        TransformDefinition
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