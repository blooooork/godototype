using Godot;
using blendporter.definition;
using blendporter.service;
using godototype.world;

namespace godototype.objects.player.ragdoll;

public partial class BalanceController : Node, IBalanceable
{
    public enum BalanceState { Standing, Stumbling, Fallen, GettingUp }

    [Export] public float UprightTorqueStiffness { get; set; } = 12f;
    [Export] public float UprightTorqueDamping   { get; set; } = 4f;
    [Export] public float VelocityLeanFactor     { get; set; } = 0.08f;
    [Export] public float MoveForce              { get; set; } = 5f;
    [Export] public float StumbleAngleDeg        { get; set; } = 55f;
    [Export] public float RecoveryImpulse        { get; set; } = 8f;
    [Export] public float RotateTorque           { get; set; } = 3f;

    public BalanceState State { get; private set; } = BalanceState.Standing;

    private RigidBody3D _lTorso;
    private bool        _enabled;
    private Vector3     _inputDir;
    private float       _rotateDir;

    public void Init(RigidBody3D lTorso)
    {
        _lTorso  = lTorso;
        _enabled = true;
    }

    public override void _EnterTree()  => BalanceManager.Register(this);
    public override void _ExitTree()   => BalanceManager.Unregister(this);

    public bool IsValid() => IsInsideTree() && _lTorso != null && IsInstanceValid(_lTorso);

    public void ApplyBalance(double delta)
    {
        // TODO: implement upright torque + move force
    }

    public void Enable()  => _enabled = true;
    public void Disable() => _enabled = false;

    public void StandUp()
    {
        // TODO: recovery impulse + state transition
    }

    public void SetInputDir(Vector3 dir, float rotate = 0f)
    {
        _inputDir  = dir;
        _rotateDir = rotate;
    }

    public void OnJump()
    {
        // TODO: jump torque
    }
}
