using Godot;
using blendporter.definition;
using blendporter.service;
using godototype.world;
using System.Collections.Generic;

namespace godototype.objects.player.ragdoll;

public partial class BalanceController : Node, IBalanceable
{
    public enum BalanceState { Standing, Stumbling, Fallen, GettingUp }

    public float PitchRollStiffness { get; set; } = 30f;
    public float PitchRollDamping   { get; set; } = 10f;
    public float YawDamping         { get; set; } = 20f;
    public float VelocityLean       { get; set; } = 0.08f;
    public float MoveForce          { get; set; } = 5f;
    public float StumbleAngle       { get; set; } = 55f;
    public float RecoveryImpulse    { get; set; } = 8f;
    public float RotateTorque       { get; set; } = 3f;

    public BalanceState State { get; private set; } = BalanceState.Standing;

    private RigidBody3D              _lTorso;
    private RigidBody3D              _uTorso;
    private IReadOnlyList<RigidBody3D> _bodies;
    private IReadOnlyList<RigidBody3D> _balanceBodies;
    private bool                     _enabled;
    private Vector3                  _inputDir;
    private float                    _rotateDir;

    // IK solvers — set by RagdollCharacter after construction
    private LegIK _leftLegIK;
    private LegIK _rightLegIK;


    // Smoothed error — exponential moving average of the raw tilt axis.
    // Prevents the controller reacting to frame-by-frame physics noise.
    private Vector3 _smoothedError = Vector3.Zero;

    public float ErrorSmoothing { get; set; } = 0.25f; // lerp alpha per tick: lower = smoother
    public float TiltDeadzone   { get; set; } = 1.5f;  // degrees: no correction below this

    private const bool LogEnabled = false;

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
    /// Bodies that receive upright torque each physics step: torso segments, head, upper arms.
    /// Must be set separately from SetBodies — torque should not apply to distal limbs.
    /// </summary>
    public void SetBalanceBodies(IReadOnlyList<RigidBody3D> balanceBodies) => _balanceBodies = balanceBodies;

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

        // ── Every physics step ────────────────────────────────────────────────
        _leftLegIK?.Solve();
        _rightLegIK?.Solve();

        // Upright torque — stiffness distributed across all torso segments so each
        // resists tipping independently. Damping applied only to _lTorso: applying
        // per-body damping to multiple spring-connected bodies creates asymmetric
        // internal joint forces that spin the whole character in yaw.
        if (_balanceBodies != null && GodotObject.IsInstanceValid(_lTorso))
        {
            // Basis.X is world-up when standing (capsule rotated 90° in scene).
            var currentUp = _lTorso.GlobalTransform.Basis.X;
            var rawError  = currentUp.Cross(Vector3.Up);

            // Low-pass filter: blend toward raw error each tick.
            // Smooths out frame-by-frame physics noise so the controller reacts
            // to the trend rather than instantaneous jitter.
            _smoothedError = _smoothedError.Lerp(rawError, ErrorSmoothing);

            // Deadzone: if tilt is negligible, apply no stiffness correction.
            // Avoids micro-correcting at near-upright where error flips sign each frame.
            var deadzoneRad = Mathf.Sin(Mathf.DegToRad(TiltDeadzone));
            var errorAxis   = _smoothedError.Length() > deadzoneRad ? _smoothedError : Vector3.Zero;

            foreach (var seg in _balanceBodies)
            {
                if (!GodotObject.IsInstanceValid(seg)) continue;

                // Stiffness: pull upright
                seg.ApplyTorque(PitchRollStiffness * errorAxis);

                // Pitch/roll damping — strip yaw component so it doesn't create
                // asymmetric cross-axis torque through the joints.
                var pitchRollVel = seg.AngularVelocity
                                 - seg.AngularVelocity.Dot(Vector3.Up) * Vector3.Up;
                seg.ApplyTorque(-PitchRollDamping * pitchRollVel);

                // Yaw damping on every body — joints transmit yaw between segments,
                // so damping only _lTorso lets the others spin and pump yaw back in.
                var yawVel = seg.AngularVelocity.Dot(Vector3.Up) * Vector3.Up;
                seg.ApplyTorque(-YawDamping * yawVel);
            }
        }


        // ── Data gathering — log every 30 physics ticks (~0.5 s) ─────────────
        if (!LogEnabled || ++_logTick % 30 != 0) return;

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
        var tiltState  = tiltDeg < StumbleAngle ? "upright" : "stumbling";

        PluginLogger.Log(LogLevel.Debug,
            $"[Balance] state={State} tilt={tiltDeg:F1}° spine={spineAngleDeg:F1}° ({tiltState}) | " +
            $"angVel={angVel:F2} linVel={linVel:F2} | " +
            $"comPos={comPos:F2} comVel={comVel:F2} | " +
            $"support={support} | input={_inputDir:F2} rotate={_rotateDir:F2} | " +
            $"totalMass={totalMass:F2}");

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