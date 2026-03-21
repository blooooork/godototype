namespace blendporter.definition;

public class PropertyDefinitions
{
    public static readonly PropertyDefinition GravityScaleDefinition = new (
        CustomNames.GravityScale,
        ConverterDefinitions.FloatConverter,
        ApplicatorDefinitions.GravityScaleApplicator
    );
    public static readonly PropertyDefinition MassDefinition = new (
        CustomNames.Mass, 
        ConverterDefinitions.FloatConverter, 
        ApplicatorDefinitions.MassApplicator
    );
    
    public static readonly PropertyDefinition TransformDefinition = new (
        CustomNames.Transform,
        ConverterDefinitions.TransformConverter,
        ApplicatorDefinitions.TransformApplicator
    );

    public static readonly PropertyDefinition PositionDefinition = new (
        CustomNames.ResetPosition,
        ConverterDefinitions.Vector3Converter,
        ApplicatorDefinitions.PositionApplicator
        );
}