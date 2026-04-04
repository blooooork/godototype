using Godot;
using blendporter.definition;
using blendporter.service;
using godototype.world;
using System.Collections.Generic;

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

    private RigidBody3D              _lTorso;
    private RigidBody3D              _uTorso;
    private IReadOnlyList<RigidBody3D> _bodies;
    private bool                     _enabled;
    private Vector3                  _inputDir;
    private float                    _rotateDir;

    // IK solvers — set by RagdollCharacter after construction
    private LegIK _leftLegIK;
    private LegIK _rightLegIK;

    private int _logTick;

    public void Init(RigidBody3D lTorso, RigidBody3D uTorso)
    {
        _lTorso  = lTorso;
        _uTorso  = uTorso;
        _enabled = true;
    }

    /// <summary>
    /// Provides the full body list for CoM calculation.
    /// Call after Init, from RagdollCharacter.GetTorsoNodes().
    /// </summary>
    public void SetBodies(IReadOnlyList<RigidBody3D> bodies) => _bodies = bodies;

    /// <summary>
    /// Registers the IK solvers so BalanceController can tick them each physics step.
    /// </summary>
    public void SetLegIK(LegIK left, LegIK right)
    {
        _leftLegIK  = left;
        _rightLegIK = right;
    }

    public override void _EnterTree() => BalanceManager.Register(this);
    public override void _ExitTree()  => BalanceManager.Unregister(this);

    public bool IsValid() => IsInsideTree() && _lTorso != null && IsInstanceValid(_lTorso);

    public void ApplyBalance(double delta)
    {
        if (!_enabled || !IsValid()) return;

        // ── Tick IK solvers ───────────────────────────────────────────────────
        // These must run every physics step regardless of log cadence.
        _leftLegIK?.Solve();
        _rightLegIK?.Solve();

        // ── Data gathering — log every 30 physics ticks (~0.5 s) ─────────────
        if (++_logTick % 30 != 0) return;

        // Torso orientation — local X is world-up when standing (capsule rotated 90° in scene)
        var torsoUp  = _lTorso.GlobalTransform.Basis.X;
        var tiltDeg  = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(torsoUp.Dot(Vector3.Up), -1f, 1f)));

        // Spine vector sanity check — geometry-independent, should agree with tiltDeg
        var spineAngleDeg = 0f;
        if (_uTorso != null && GodotObject.IsInstanceValid(_uTorso))
        {
            var spine = (_uTorso.GlobalPosition - _lTorso.GlobalPosition).Normalized();
            spineAngleDeg = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(spine.Dot(Vector3.Up), -1f, 1f)));
        }
        var angVel   = _lTorso.AngularVelocity;
        var linVel   = _lTorso.LinearVelocity;

        // CoM — mass-weighted average across all bodies
        var comPos   = Vector3.Zero;
        var comVel   = Vector3.Zero;
        var totalMass = 0f;
        if (_bodies != null)
        {
            foreach (var b in _bodies)
            {
                if (!GodotObject.IsInstanceValid(b)) continue;
                totalMass += b.Mass;
                comPos    += b.GlobalPosition * b.Mass;
                comVel    += b.LinearVelocity * b.Mass;
            }
            if (totalMass > 0f) { comPos /= totalMass; comVel /= totalMass; }
        }
        else
        {
            // Fallback until SetBodies is called
            comPos = _lTorso.GlobalPosition;
            comVel = linVel;
        }

        // Support polygon — foot ground state comes from LegIK
        var lGrounded = _leftLegIK?.IsGrounded  ?? false;
        var rGrounded = _rightLegIK?.IsGrounded ?? false;
        var support   = lGrounded && rGrounded ? "both"
                      : lGrounded              ? "left only"
                      : rGrounded              ? "right only"
                      :                          "AIRBORNE";

        // Tilt state classification (using exported threshold)
        var stumbleRad = Mathf.DegToRad(StumbleAngleDeg);
        var tiltState  = tiltDeg < StumbleAngleDeg ? "upright" : "stumbling";

        PluginLogger.Log(LogLevel.Debug,
            $"[Balance] state={State} tilt={tiltDeg:F1}° spine={spineAngleDeg:F1}° ({tiltState}) | " +
            $"angVel={angVel:F2} linVel={linVel:F2} | " +
            $"comPos={comPos:F2} comVel={comVel:F2} | " +
            $"support={support} | input={_inputDir:F2} rotate={_rotateDir:F2} | " +
            $"totalMass={totalMass:F2}");

        // TODO: implement upright torque — apply to _lTorso based on tilt + angVel
        // TODO: implement move force — apply horizontal force based on _inputDir
        // TODO: implement stumble/fall state transitions based on tiltDeg
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
        // TODO: jump torque — brief upward angular impulse on _lTorso
    }
}