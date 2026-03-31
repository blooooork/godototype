using Godot;
using System.Collections.Generic;

namespace godototype.world;

/// <summary>
/// Autoload singleton that applies gravity to all registered bodies.
/// Consumers register themselves and provide a callback to receive the result.
/// 
/// Usage:
///   GravitySystem.Register(this);
///   GravitySystem.Unregister(this);
/// </summary>
public partial class GravityManager : Node
{
    public static GravityManager Instance { get; private set; }

    private static readonly List<IGravityBody> _bodies = new();

    public override void _EnterTree()
    {
        Instance = this;
        ProcessPriority = -90;
    }

    public override void _PhysicsProcess(double delta)
    {
        var gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        var dt = (float)delta;

        for (var i = _bodies.Count - 1; i >= 0; i--)
        {
            var body = _bodies[i];
            if (!body.IsValid()) { _bodies.RemoveAt(i); continue; }
            body.ApplyGravity(gravity, dt);
        }
    }

    public static void Register(IGravityBody body)
    {
        if (!_bodies.Contains(body))
            _bodies.Add(body);
    }

    public static void Unregister(IGravityBody body)
    {
        _bodies.Remove(body);
    }
}
