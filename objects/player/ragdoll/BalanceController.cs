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

    private RigidBody3D                _lTorso;
    private RigidBody3D                _uTorso;
    private IReadOnlyList<RigidBody3D> _bodies;
    private IReadOnlyList<RigidBody3D> _balanceBodies;
    private bool                       _enabled;
    private Vector3                    _inputDir;
    private float                      _rotateDir;

    // IK solvers — set by RagdollCharacter after construction
    private LegIK _leftLegIK;
    private LegIK _rightLegIK;

    // Upright anchor joint — the implicit spring that replaces all ApplyTorque balance calls.
    // A frozen RigidBody3D sits at the torso's spawn orientation. A Generic6DOFJoint3D with
    // angular spring on Y and Z pulls _lTorso back to match it. The spring is solved by the
    // constraint solver (implicit) — unconditionally stable at any stiffness, no timestep
    // instability. Joint X = world-up axis in anchor frame = yaw → left free.
    private RigidBody3D        _anchor;
    private Generic6DofJoint3D _balanceJoint;

    // Smoothed tilt error — kept for state machine use (stumble/fall detection), not for torque.
    private Vector3 _smoothedError = Vector3.Zero;
    public float ErrorSmoothing { get; set; } = 0.25f;
    public float TiltDeadzone   { get; set; } = 1.5f;

    private const bool  LogEnabled        = false;
    private const float JitterLogThreshold = 15f;

    private Dictionary<RigidBody3D, Vector3> _prevAngVel  = new();
    private Dictionary<RigidBody3D, float>   _jitterAccum = new();
    private int _jitterTick;
    private int _logTick;

    public void Init(RigidBody3D lTorso, RigidBody3D uTorso)
    {
        _lTorso  = lTorso;
        _uTorso  = uTorso;
        _enabled = true;
        CreateBalanceJoint();
    }

    private void CreateBalanceJoint()
    {
        // Anchor: frozen, no collision, oriented to match the torso's upright basis.
        // equilibrium=0 on the joint therefore means "torso is upright".
        _anchor = new RigidBody3D
        {
            Name           = "BalanceAnchor",
            Freeze         = true,
            CollisionLayer = 0,
            CollisionMask  = 0,
        };

        _balanceJoint = new Generic6DofJoint3D { Name = "BalanceJoint" };

        // Add as siblings of BalanceController under RagdollCharacter
        var parent = (Node3D)GetParent();
        parent.AddChild(_anchor);
        parent.AddChild(_balanceJoint);

        // Orient anchor to match lTorso's current upright orientation.
        // Position doesn't matter (linear DOFs are free) but keep it tidy.
        _anchor.GlobalTransform      = new Transform3D(_lTorso.GlobalTransform.Basis, _lTorso.GlobalPosition);
        _balanceJoint.GlobalTransform = _anchor.GlobalTransform;

        // Wire to anchor → lTorso using absolute paths
        _balanceJoint.NodeA = _anchor.GetPath();
        _balanceJoint.NodeB = _lTorso.GetPath();

        // Linear: very large limits so the torso can translate freely — we only want
        // angular correction. Using large limits instead of disabling to ensure
        // consistent behaviour across physics backends.
        _balanceJoint.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearLimit, true);
        _balanceJoint.SetFlagY(Generic6DofJoint3D.Flag.EnableLinearLimit, true);
        _balanceJoint.SetFlagZ(Generic6DofJoint3D.Flag.EnableLinearLimit, true);
        const float free = 1000f;
        _balanceJoint.SetParamX(Generic6DofJoint3D.Param.LinearLowerLimit, -free);
        _balanceJoint.SetParamX(Generic6DofJoint3D.Param.LinearUpperLimit,  free);
        _balanceJoint.SetParamY(Generic6DofJoint3D.Param.LinearLowerLimit, -free);
        _balanceJoint.SetParamY(Generic6DofJoint3D.Param.LinearUpperLimit,  free);
        _balanceJoint.SetParamZ(Generic6DofJoint3D.Param.LinearLowerLimit, -free);
        _balanceJoint.SetParamZ(Generic6DofJoint3D.Param.LinearUpperLimit,  free);

        // Angular X: free — in anchor frame this axis = world-up = yaw. Player controls yaw.
        _balanceJoint.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularLimit,  false);
        _balanceJoint.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularSpring, false);

        // Angular Y and Z: spring toward 0 = upright. Implicit, stable at any stiffness.
        _balanceJoint.SetFlagY(Generic6DofJoint3D.Flag.EnableAngularLimit,  false);
        _balanceJoint.SetFlagY(Generic6DofJoint3D.Flag.EnableAngularSpring, true);
        _balanceJoint.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness,        PitchRollStiffness);
        _balanceJoint.SetParamY(Generic6DofJoint3D.Param.AngularSpringDamping,          PitchRollDamping);
        _balanceJoint.SetParamY(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);

        _balanceJoint.SetFlagZ(Generic6DofJoint3D.Flag.EnableAngularLimit,  false);
        _balanceJoint.SetFlagZ(Generic6DofJoint3D.Flag.EnableAngularSpring, true);
        _balanceJoint.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness,        PitchRollStiffness);
        _balanceJoint.SetParamZ(Generic6DofJoint3D.Param.AngularSpringDamping,          PitchRollDamping);
        _balanceJoint.SetParamZ(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
    }

    /// <summary>
    /// Provides the full body list for CoM calculation.
    /// Call after Init, from RagdollCharacter.GetTorsoNodes().
    /// </summary>
    public void SetBodies(IReadOnlyList<RigidBody3D> bodies) => _bodies = bodies;

    /// <summary>
    /// Bodies monitored for jitter detection. Typically just the torso segments.
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

        _leftLegIK?.Solve();
        _rightLegIK?.Solve();

        // Upright correction is now handled entirely by the implicit joint spring (_balanceJoint).
        // No ApplyTorque calls here — the constraint solver applies the spring force stably.

        // Keep smoothed tilt error updated for future state machine (stumble/fall detection).
        if (GodotObject.IsInstanceValid(_lTorso))
        {
            var rawError = _lTorso.GlobalTransform.Basis.X.Cross(Vector3.Up);
            _smoothedError = _smoothedError.Lerp(rawError, ErrorSmoothing);
        }

        // Jitter sampling — angular velocity change per tick, summed over 60 ticks.
        if (_balanceBodies != null)
        {
            foreach (var seg in _balanceBodies)
            {
                if (!GodotObject.IsInstanceValid(seg)) continue;
                var prev = _prevAngVel.TryGetValue(seg, out var p) ? p : seg.AngularVelocity;
                _jitterAccum[seg] = (_jitterAccum.TryGetValue(seg, out var acc) ? acc : 0f)
                                  + (seg.AngularVelocity - prev).Length();
                _prevAngVel[seg] = seg.AngularVelocity;
            }
        }

        // ── Jitter report — once per second ──────────────────────────────────
        if (++_jitterTick % 60 == 0 && _jitterAccum.Count > 0)
        {
            foreach (var (seg, total) in _jitterAccum)
            {
                if (total > JitterLogThreshold)
                    GD.Print($"[Jitter] {seg.Name}  score={total:F1}  angVel={seg.AngularVelocity.Length():F2} rad/s");
            }
            _jitterAccum.Clear();
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