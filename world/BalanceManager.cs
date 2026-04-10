using Godot;
using System.Collections.Generic;

namespace godototype.world;

/// <summary>
/// Autoload singleton that applies balance torques to registered bodies.
/// Runs at priority -85: after GravityManager (-90), before PoseManager (-80).
/// This ordering ensures balance torque lands before joint springs respond.
///
/// Usage:
///   BalanceManager.Register(this);
///   BalanceManager.Unregister(this);
/// </summary>
public partial class BalanceManager : Node
{
    public static BalanceManager Instance { get; private set; }

    private static readonly List<IBalanceable> _bodies = new();

    public override void _EnterTree()
    {
        Instance = this;
        ProcessPriority = -85;
    }

    public override void _PhysicsProcess(double delta)
    {
        for (var i = _bodies.Count - 1; i >= 0; i--)
        {
            var body = _bodies[i];
            if (!body.IsValid()) { _bodies.RemoveAt(i); continue; }
            body.ApplyBalance(delta);
        }
    }

    public static void Register(IBalanceable body)
    {
        if (!_bodies.Contains(body))
            _bodies.Add(body);
    }

    public static void Unregister(IBalanceable body)
    {
        _bodies.Remove(body);
    }
}
