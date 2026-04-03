using Godot;
using System;
using System.Collections.Generic;

namespace godototype.world;

/// <summary>
/// Autoload singleton that drives per-physics-tick pose callbacks.
/// Anything that needs to apply spring forces toward a rest pose registers a callback here.
///
/// Usage:
///   PoseManager.Register(ApplyPoseForces);
///   PoseManager.Unregister(ApplyPoseForces);
/// </summary>
public partial class PoseManager : Node
{
    public static PoseManager Instance { get; private set; }

    private static readonly List<Action<double>> _callbacks = new();

    public override void _EnterTree()
    {
        Instance = this;
        ProcessPriority = -80;
    }

    public override void _PhysicsProcess(double delta)
    {
        for (var i = 0; i < _callbacks.Count; i++)
            _callbacks[i](delta);
    }

    public static void Register(Action<double> callback)
    {
        if (!_callbacks.Contains(callback))
            _callbacks.Add(callback);
    }

    public static void Unregister(Action<double> callback)
    {
        _callbacks.Remove(callback);
    }
}
