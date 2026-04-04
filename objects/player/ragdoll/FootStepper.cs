using Godot;
using System;
using godototype.world;

namespace godototype.objects.player.ragdoll;

public partial class FootStepper : Node
{
    public enum StepState { Stance, Swing }

    [Export] public RigidBody3D        Foot         { get; set; }
    [Export] public FootStepper        OtherStepper { get; set; }
    [Export] public Generic6DofJoint3D HipJoint     { get; set; }
    [Export] public Generic6DofJoint3D KneeJoint    { get; set; }
    [Export] public Generic6DofJoint3D AnkleJoint   { get; set; }

    [Export] public float StepDistance         { get; set; } = 0.35f;
    [Export] public float StepHeight           { get; set; } = 0.15f;
    [Export] public float StepDuration         { get; set; } = 0.25f;
    [Export] public float StanceSticktionForce { get; set; } = 60f;
    [Export] public float SwingStiffness       { get; set; } = 200f;
    [Export] public float SwingDamping         { get; set; } = 20f;
    [Export] public float LateralOffset        { get; set; } = 0.2f;

    public StepState State { get; private set; } = StepState.Stance;

    private RigidBody3D    _lTorso;
    private RigidBody3D    _mTorso;
    private Vector3        _inputDir;
    private bool           _enabled;
    private Action<double> _callback;

    public void Init(RigidBody3D lTorso, RigidBody3D mTorso, float springStiffness, float springDamping)
    {
        _lTorso  = lTorso;
        _mTorso  = mTorso;
        _enabled = true;
    }

    public override void _Ready()
    {
        _callback = OnPhysicsTick;
        PoseManager.Register(_callback);
    }

    public override void _ExitTree() => PoseManager.Unregister(_callback);

    public void SetInputDir(Vector3 dir) => _inputDir = dir;

    public void Enable()  => _enabled = true;
    public void Disable() => _enabled = false;

    private void OnPhysicsTick(double delta)
    {
        // TODO: stance sticktion + swing spring toward foot target
    }
}
