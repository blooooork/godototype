using System.Collections.Generic;
using System;
using Godot;
using System.Linq;

namespace blendporter.definition;

// TODO Get it on IRegistry
//          See what UI interfacing can be done with all of that
//          If we end up allowing definitions in the UI along with our defined things below
//              These along with the ones defined in SettingRegistry need to be moved to Defaults
//              REALLY the solution is to rename definitions package to defaults package
//                  Rename what is Defaults in constant right now to something else
//              with package renamed as default
//                  ApplicatorDefaults
//                  ConverterDefaults
//                  PropertyDefaults
//                  SettingDefaults
public static class PropertyRegistry
{
    // Property Definitions
    private static readonly PropertyDefinition GravityScaleDefinition = new (
        Names.GravityScale,
        typeof(RigidBody3D),
        ConverterDefinitions.FloatConverter,
        ApplicatorDefinitions.GravityScaleApplicator
    );
    private static readonly PropertyDefinition MassDefinition = new (
        Names.Mass, 
        typeof(RigidBody3D),
        ConverterDefinitions.FloatConverter, 
        ApplicatorDefinitions.MassApplicator
    );
    
    private static readonly PropertyDefinition TransformDefinition = new (
        Names.Transform,
        typeof(Node3D),
        ConverterDefinitions.TransformConverter,
        ApplicatorDefinitions.TransformApplicator
    );

    private static readonly PropertyDefinition PositionDefinition = new (
        Names.ResetPosition,
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

    public static readonly Dictionary<Type, List<PropertyDefinition>> PropertyDefinitions =
        PropertyList.GroupBy(d => d.Type)
            .ToDictionary(g => g.Key, g => g.ToList());

    #nullable enable
    public static PropertyDefinition? GetPropertyDefinition(string customName)
    {
        return PropertyList.FirstOrDefault(d => d.Name.ToString() == customName);
    }
    #nullable disable
}