using Godot;
using godototype.world;

namespace godototype.objects.player.ragdoll.limbs;

public abstract partial class Limb : Node3D
{
    [Export] public RigidBody3D Anchor    { get; set; }
    [Export] public bool        PinToWorld { get; set; }

    protected abstract string AttachJointPath { get; }

    private Generic6DofJoint3D _attachJoint;

    public override void _Ready()
    {
        _attachJoint = GetNode<Generic6DofJoint3D>(AttachJointPath);

        if (Anchor != null)
        {
            _attachJoint.NodeA = _attachJoint.GetPathTo(Anchor);
            JointWatchManager.Watch(_attachJoint, Anchor, OnSevered);
        }
        else if (!PinToWorld)
        {
            _attachJoint.QueueFree();
        }

        base._Ready();
    }

    public override void _ExitTree()
    {
        if (_attachJoint != null && IsInstanceValid(_attachJoint))
            JointWatchManager.Unwatch(_attachJoint);
        base._ExitTree();
    }

    public void Detach()
    {
        if (_attachJoint == null || !IsInstanceValid(_attachJoint)) return;
        JointWatchManager.Unwatch(_attachJoint);
        _attachJoint.QueueFree();
        OnSevered();
    }

    private void OnSevered()
    {
        _attachJoint = null;
        Anchor       = null;
    }
}