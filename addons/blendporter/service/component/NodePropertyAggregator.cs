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
