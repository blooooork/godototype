using Godot;
using System;
using System.Collections.Generic;

namespace godototype.world;

/// <summary>
/// Autoload singleton that watches joints for invalid anchors.
/// When a watched anchor node is freed, the joint is queued for deletion
/// and the registered callback is fired.
///
/// Usage:
///   JointWatchManager.Watch(joint, anchorNode, OnSevered);
///   JointWatchManager.Unwatch(joint);
/// </summary>
public partial class JointWatchManager : Node
{
    public static JointWatchManager Instance { get; private set; }

    private record struct WatchEntry(Joint3D Joint, GodotObject Anchor, Action OnSevered);
    private static readonly List<WatchEntry> _watched = new();

    public override void _EnterTree()
    {
        Instance = this;
        ProcessPriority = -70;
    }

    public override void _PhysicsProcess(double delta)
    {
        for (var i = _watched.Count - 1; i >= 0; i--)
        {
            var entry = _watched[i];
            if (!IsInstanceValid(entry.Anchor) || !IsInstanceValid(entry.Joint))
            {
                if (IsInstanceValid(entry.Joint))
                    entry.Joint.QueueFree();
                entry.OnSevered?.Invoke();
                _watched.RemoveAt(i);
            }
        }
    }

    public static void Watch(Joint3D joint, GodotObject anchor, Action onSevered = null)
    {
        _watched.Add(new WatchEntry(joint, anchor, onSevered));
    }

    public static void Unwatch(Joint3D joint)
    {
        _watched.RemoveAll(e => e.Joint == joint);
    }
}
