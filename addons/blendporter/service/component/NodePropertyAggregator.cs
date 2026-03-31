#nullable enable
using blendporter.definition;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace blendporter.service.component;

public record PropertyKey(Type SetterType, PropertyRestriction? Restriction = null);

public static class NodePropertyAggregator
{
    private static readonly Dictionary<Type, IReadOnlyDictionary<PropertyKey, IReadOnlyList<PropertyInfo>>> _cache = new();

    public static IReadOnlyDictionary<PropertyKey, IReadOnlyList<PropertyInfo>> GetSettableProperties(Type nodeType)
    {
        if (_cache.TryGetValue(nodeType, out var cached))
            return cached;

        var built = Build(nodeType);
        _cache[nodeType] = built;
        return built;
    }

    private static IReadOnlyDictionary<PropertyKey, IReadOnlyList<PropertyInfo>> Build(Type nodeType)
    {
        return nodeType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetSetMethod(false) != null)
            .SelectMany(p => KeysFor(p.PropertyType).Select(key => (key, prop: p)))
            .GroupBy(x => x.key, x => x.prop)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<PropertyInfo>)g.ToList().AsReadOnly()
            );
    }

    // Maps a Blender/Variant value type (plus optional array length) to the PropertyKey
    // used to look up matching settable Godot properties.
    public static PropertyKey? GetPropertyKey(Godot.Variant.Type variantType, int? arrayLength = null)
    {
        return variantType switch
        {
            Godot.Variant.Type.Float              => new PropertyKey(typeof(float)),
            Godot.Variant.Type.Int                => new PropertyKey(typeof(int)),
            Godot.Variant.Type.Bool               => new PropertyKey(typeof(bool)),
            Godot.Variant.Type.String             => new PropertyKey(typeof(string)),
            Godot.Variant.Type.Vector2            => new PropertyKey(typeof(Vector2)),
            Godot.Variant.Type.Vector3            => new PropertyKey(typeof(Vector3)),
            Godot.Variant.Type.Color              => new PropertyKey(typeof(Color)),
            Godot.Variant.Type.Transform3D        => new PropertyKey(typeof(Transform3D)),
            Godot.Variant.Type.PackedFloat32Array => arrayLength switch
            {
                2  => new PropertyKey(typeof(Vector2),    new LengthRestriction(2)),
                3  => new PropertyKey(typeof(Vector3),    new LengthRestriction(3)),
                4  => new PropertyKey(typeof(Color),      new LengthRestriction(4)),
                12 => new PropertyKey(typeof(Transform3D), new LengthRestriction(12)),
                _  => null
            },
            _ => null
        };
    }

    public static string BuildEnumString(NodeProperty prop)
    {
        const string none = "None";
        var key = GetPropertyKey(prop.VariantType, prop.ArrayLength);
        if (key == null) return none;
        var settable = GetSettableProperties(prop.NodeType);
        if (!settable.TryGetValue(key, out var matches) || matches.Count == 0) return none;
        return none + "," + string.Join(",", matches.Select(p => p.Name));
    }

    public static object? ConvertVariant(Variant value, Type targetType)
    {
        if (targetType == typeof(float))  return value.AsSingle();
        if (targetType == typeof(double)) return (double)value.AsSingle();
        if (targetType == typeof(int))    return value.AsInt32();
        if (targetType == typeof(long))   return (long)value.AsInt64();
        if (targetType == typeof(bool))   return value.AsBool();
        if (targetType == typeof(string)) return value.AsString();
        if (targetType == typeof(Vector2))
        {
            if (value.VariantType == Godot.Variant.Type.PackedFloat32Array)
            {
                var a = value.As<float[]>();
                return a.Length >= 2 ? new Vector2(a[0], a[1]) : null;
            }
            return value.AsVector2();
        }
        if (targetType == typeof(Vector3))
        {
            if (value.VariantType == Godot.Variant.Type.PackedFloat32Array)
            {
                var a = value.As<float[]>();
                return a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : null;
            }
            return value.AsVector3();
        }
        if (targetType == typeof(Color))
        {
            if (value.VariantType == Godot.Variant.Type.PackedFloat32Array)
            {
                var a = value.As<float[]>();
                return a.Length >= 4 ? new Color(a[0], a[1], a[2], a[3]) : null;
            }
            return value.AsColor();
        }
        if (targetType == typeof(Transform3D))
        {
            if (value.VariantType == Godot.Variant.Type.PackedFloat32Array)
            {
                var a = value.As<float[]>();
                if (a.Length < 12) return null;
                return new Transform3D(
                    new Basis(new Vector3(a[0], a[1], a[2]), new Vector3(a[3], a[4], a[5]), new Vector3(a[6], a[7], a[8])),
                    new Vector3(a[9], a[10], a[11])
                );
            }
            return value.AsTransform3D();
        }
        return null;
    }

    // Maps a property's setter type to all keys under which it should be discoverable.
    // Types that Blender exports as PackedFloat32Array get an additional keyed entry
    // with a LengthRestriction so callers can match by incoming array length.
    private static IEnumerable<PropertyKey> KeysFor(Type type)
    {
        yield return new PropertyKey(type);

        if (type == typeof(Vector2))
            yield return new PropertyKey(type, new LengthRestriction(2));
        else if (type == typeof(Vector3))
            yield return new PropertyKey(type, new LengthRestriction(3));
        else if (type == typeof(Color))
            yield return new PropertyKey(type, new LengthRestriction(4));
        else if (type == typeof(Transform3D))
            yield return new PropertyKey(type, new LengthRestriction(12));
    }
}
