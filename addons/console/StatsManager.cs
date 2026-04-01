using System;
using System.Collections.Generic;
using Godot;

namespace godotconsole;

public static class StatsManager
{
    private static readonly Dictionary<Node, Dictionary<string, Func<string>>> _stats = new();

    public static Node FocusedEntity { get; set; }

    public static void Register(Node owner, string name, Func<string> provider)
    {
        if (!_stats.ContainsKey(owner)) _stats[owner] = new();
        _stats[owner][name] = provider;
    }

    public static void Unregister(Node owner) => _stats.Remove(owner);

    public static IEnumerable<(string Name, string Value)> GetStats(Node owner)
    {
        if (owner == null || !_stats.TryGetValue(owner, out var dict)) yield break;
        foreach (var (name, provider) in dict)
            yield return (name, provider());
    }
}
