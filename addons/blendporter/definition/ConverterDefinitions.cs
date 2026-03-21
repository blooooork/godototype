using System;
using Godot;

namespace blendporter.definition;

public static class ConverterDefinitions
{
    public static readonly Func<Variant, object> FloatConverter = v => v.AsSingle();
    public static readonly Func<Variant, object> TransformConverter = fL =>
    {
        var v = fL.AsFloat32Array();
        if (v.Length != 12)
            return null;
        return new Transform3D(
            new Basis(
                new Vector3(v[0], v[1], v[2]),
                new Vector3(v[3], v[4], v[5]),
                new Vector3(v[6], v[7], v[8])
            ),
            new Vector3(v[9], v[10], v[11])
        );
    };

    public static readonly Func<Variant, object> Vector3Converter = fL =>
    {
        var v = fL.AsFloat32Array();
        if (v.Length != 3)
            return null;
        return new Vector3(v[0], v[1], v[2]);
    };
}