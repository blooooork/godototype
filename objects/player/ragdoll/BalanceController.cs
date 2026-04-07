using Godot;
using blendporter.definition;
using blendporter.service;
using godototype.world;
using System.Collections.Generic;

namespace godototype.objects.player.ragdoll;

public partial class BalanceController : Node, IBalanceable
{
    public enum BalanceState { Standing, Fallen }

    public float PitchRollStiffness { get; set; } = 30f;
    public float PitchRollDamping   { get; set; } = 10f;
    public float VelocityLean       { get; set; } = 0.08f;
    public float MoveForce          { get; set; } = 5f;
    public float StumbleAngle       { get; set; } = 55f;

    public BalanceState State { get; private set; } = BalanceState.Standing;

    private RigidBody3D                _lTorso;
    private RigidBody3D                _uTorso;
    private IReadOnlyList<RigidBody3D> _bodies;
    private IReadOnlyList<RigidBody3D> _balanceBodies;
    private bool                       _enabled;
    private Vector3                    _inputDir;
    private float                      _rotateDir;
    private FootStepper                _footStepper;

    // Upright anchor joint — a frozen RigidBody3D sits at the torso's spawn orientation.
    // A Generic6DofJoint3D with angular spring on Y and Z pulls _lTorso back upright.
    // Solved implicitly by the constraint solver — stable at any stiffness.
    // Joint X = world-up axis in anchor frame = yaw, damping-only to resist spin from steps.
    private RigidBody3D        _anchor;
    private Generic6DofJoint3D _balanceJoint;
    private Basis              _anchorRestBasis;

    private const bool  LogEnabled         = true;
    private const float JitterLogThreshold = 15f;

    private Dictionary<RigidBody3D, Vector3> _prevAngVel  = new();
    private Dictionary<RigidBody3D, float>   _jitterAccum = new();
    private int _jitterTick;
    private int _logTick;

    public void Init(RigidBody3D lTorso, RigidBody3D uTorso, FootStepper footStepper = null)
    {
        _lTorso      = lTorso;
        _uTorso      = uTorso;
        _footStepper = footStepper;
        _enabled     = true;
        CreateBalanceJoint();
    }

    private void CreateBalanceJoint()
    {
        _anchor = new RigidBody3D
        {
            Name           = "BalanceAnchor",
            Freeze         = true,
            CollisionLayer = 0,
            CollisionMask  = 0
        };

        _balanceJoint = new Generic6DofJoint3D { Name = "BalanceJoint" };

        var parent = (Node3D)GetParent();
        parent.AddChild(_anchor);
        parent.AddChild(_balanceJoint);

        _anchor.GlobalTransform       = new Transform3D(_lTorso.GlobalTransform.Basis, _lTorso.GlobalPosition);
        _balanceJoint.GlobalTransform = _anchor.GlobalTransform;
        _anchorRestBasis              = _anchor.GlobalTransform.Basis;

        _balanceJoint.NodeA = _anchor.GetPath();
        _balanceJoint.NodeB = _lTorso.GetPath();

        // Linear: large free range — translation is unrestricted, only rotation is controlled.
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

        // Yaw (joint X = world-up in anchor frame): damping only, no stiffness.
        // Resists unintended spin from asymmetric forces without locking player-driven rotation.
        _balanceJoint.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularLimit,  false);
        _balanceJoint.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularSpring, true);
        _balanceJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness,        0f);
        _balanceJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringDamping,          PitchRollDamping);
        _balanceJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);

        // Pitch and roll: spring toward 0 = upright.
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

    public void SetBodies(IReadOnlyList<RigidBody3D> bodies)        => _bodies        = bodies;
    public void SetBalanceBodies(IReadOnlyList<RigidBody3D> bodies) => _balanceBodies = bodies;

    /// <summary>
    /// The anchor's current "up" direction — the orientation the balance spring is pulling toward.
    /// At rest = world up. With forward input = tilted toward input direction.
    /// </summary>
    public Vector3 AnchorUp => IsInstanceValid(_anchor)
        ? _anchor.GlobalTransform.Basis.X
        : Vector3.Up;

    public override void _EnterTree() => BalanceManager.Register(this);
    public override void _ExitTree()  => BalanceManager.Unregister(this);

    public bool IsValid() => IsInsideTree() && _lTorso != null && IsInstanceValid(_lTorso);

    public void ApplyBalance(double delta)
    {
        if (!_enabled || !IsValid()) return;

        // Mass-weighted CoM for logging.
        var comPos    = Vector3.Zero;
        var comVel    = Vector3.Zero;
        var totalMass = 0f;
        if (_bodies != null)
        {
            foreach (var b in _bodies)
            {
                if (!IsInstanceValid(b)) continue;
                totalMass += b.Mass;
                comPos    += b.GlobalPosition * b.Mass;
                comVel    += b.LinearVelocity  * b.Mass;
            }
            if (totalMass > 0f) { comPos /= totalMass; comVel /= totalMass; }
        }

        // Lean anchor toward input — tips the CoM, causing the character to stumble forward.
        if (IsInstanceValid(_anchor))
        {
            // Collapse to full ragdoll if tilt exceeds threshold.
            var torsoUp = _lTorso.GlobalTransform.Basis.X;
            var tiltDeg = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(torsoUp.Dot(Vector3.Up), -1f, 1f)));
            if (State == BalanceState.Standing && tiltDeg > StumbleAngle)
                Collapse();
            
            Basis targetBasis;
            if (_inputDir.LengthSquared() > 0.0001f)
            {
                var leanDir   = _inputDir.Normalized();
                var leanAxis  = Vector3.Up.Cross(leanDir).Normalized();
                var leanAngle = Mathf.Min(_inputDir.Length() * VelocityLean, Mathf.DegToRad(45f));
                targetBasis   = new Basis(leanAxis, leanAngle) * _anchorRestBasis;
                
                // Directional force — lean alone is slow, force provides initial momentum.
                _lTorso.ApplyCentralForce(_inputDir * MoveForce);
            }
            else
            {
                targetBasis = _anchorRestBasis;
            }
            _anchor.GlobalTransform = new Transform3D(targetBasis, _anchor.GlobalPosition);
        }

        // Jitter sampling — angular velocity delta per tick, reported once per second.
        if (_balanceBodies != null)
        {
            foreach (var seg in _balanceBodies)
            {
                if (!IsInstanceValid(seg)) continue;
                var prev = _prevAngVel.TryGetValue(seg, out var p) ? p : seg.AngularVelocity;
                _jitterAccum[seg] = (_jitterAccum.TryGetValue(seg, out var acc) ? acc : 0f)
                                  + (seg.AngularVelocity - prev).Length();
                _prevAngVel[seg] = seg.AngularVelocity;
            }
        }
        if (++_jitterTick % 60 == 0 && _jitterAccum.Count > 0)
        {
            foreach (var (seg, total) in _jitterAccum)
            {
                if (total > JitterLogThreshold)
                    GD.Print($"[Jitter] {seg.Name}  score={total:F1}  angVel={seg.AngularVelocity.Length():F2} rad/s");
            }
            _jitterAccum.Clear();
        }

        // Log every 30 ticks (~0.5 s).
        if (!LogEnabled || ++_logTick % 30 != 0) return;

        var logTorsoUp    = _lTorso.GlobalTransform.Basis.X;
        var logTiltDeg    = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(logTorsoUp.Dot(Vector3.Up), -1f, 1f)));
        var spineAngleDeg = 0f;
        if (_uTorso != null && IsInstanceValid(_uTorso))
        {
            var spine = (_uTorso.GlobalPosition - _lTorso.GlobalPosition).Normalized();
            spineAngleDeg = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(spine.Dot(Vector3.Up), -1f, 1f)));
        }
        var tiltState = logTiltDeg < StumbleAngle ? "upright" : "stumbling";

        PluginLogger.Log(LogLevel.Debug,
            $"[<TIMED>Balance] state={State} tilt={logTiltDeg:F1}° spine={spineAngleDeg:F1}° ({tiltState}) | " +
            $"angVel={_lTorso.AngularVelocity:F2} linVel={_lTorso.LinearVelocity:F2} | " +
            $"comPos={comPos:F2} comVel={comVel:F2} | input={_inputDir:F2}");
    }

    private void Collapse()
    {
        State = BalanceState.Fallen;
        if (IsInstanceValid(_balanceJoint))
        {
            _balanceJoint.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, 0f);
            _balanceJoint.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, 0f);
        }
        _footStepper?.Disable();
    }

    public void Enable()
    {
        _enabled = true;
        if (State != BalanceState.Fallen || !IsInstanceValid(_balanceJoint))
            return;
        State = BalanceState.Standing;
        _balanceJoint.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, PitchRollStiffness);
        _balanceJoint.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, PitchRollStiffness);
        _footStepper?.Enable();
    }

    public void Disable() => _enabled = false;

    public void SetInputDir(Vector3 dir, float rotate = 0f)
    {
        _inputDir  = dir;
        _rotateDir = rotate;
    }

    public void OnJump() { }
    public void StandUp() { }
}
