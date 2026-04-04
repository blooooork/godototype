using Godot;
using System.Collections.Generic;

namespace godototype.world;

public static class SpawnRegistry
{
    private record Entry(IResettable Target, Transform3D SpawnTransform, string Name);

    private static readonly List<Entry> _entries = new();

    public static void Register(IResettable target, Transform3D spawnTransform, string name)
        => _entries.Add(new Entry(target, spawnTransform, name));

    public static void Unregister(IResettable target)
        => _entries.RemoveAll(e => e.Target == target);

    public static void ResetAll()
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
            TryReset(i);
    }

    public static bool ResetByName(string name)
    {
        var found = false;
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            if (!_entries[i].Name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) continue;
            TryReset(i);
            found = true;
        }
        return found;
    }

    private static void TryReset(int i)
    {
        var e = _entries[i];
        if (e.Target is GodotObject obj && !GodotObject.IsInstanceValid(obj))
        {
            _entries.RemoveAt(i);
            return;
        }
        e.Target.Reset(e.SpawnTransform);
    }

    public static int Count => _entries.Count;
}
