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
    public float YawDamping         { get; set; } = 10f;
    public float VelocityLean       { get; set; } = 0.08f;
    public float MoveForce          { get; set; } = 5f;
    public float StumbleAngle       { get; set; } = 55f;
    // Viscous drag applied to the lower torso's horizontal velocity when there is no input.
    // The balance joint corrects tilt but leaves translational momentum untouched — without
    // this, tiny spawn-settle velocities accumulate, drift the CoM, and trigger spurious steps.
    public float IdleBrakingForce   { get; set; } = 20f;
    public float TurnMaxSpeed       { get; set; } = 90f;
    // PD spring pulling the whole-body CoM toward the support polygon centre (planted foot midpoint).
    // Equivalent to the ankle+hip strategy in Euphoria — corrects position drift without stepping.
    public float LeanRestoreForce   { get; set; } = 0f;
    public float LeanRestoreDamping { get; set; } = 0f;

    public BalanceState State { get; private set; } = BalanceState.Standing;

    private RigidBody3D                _lTorso;
    private RigidBody3D                _uTorso;
    private IReadOnlyList<RigidBody3D> _bodies;
    private IReadOnlyList<RigidBody3D> _balanceBodies;
    private bool                       _enabled;
    private Vector2                    _rawInput;
    private float                      _rotateDir;
    private FootStepper                _footStepper;

    // Upright anchor joint — a frozen RigidBody3D sits at the torso's spawn orientation.
    // A Generic6DofJoint3D with angular spring on Y and Z pulls _lTorso back upright.
    // Solved implicitly by the constraint solver — stable at any stiffness.
    // Joint X = world-up axis in anchor frame = yaw, damping-only to resist spin from steps.
    private RigidBody3D        _anchor;
    private Generic6DofJoint3D _balanceJoint;
    private Basis              _anchorRestBasis;

    private const float JitterLogThreshold  = 15f;

    // Reactive log settings.
    // DebounceLog = true: repeated lean events fire at most once per LeanLogCooldown seconds.
    // DebounceLog = false: every lean tick is logged — useful for a single-frame investigation,
    //                      spammy for sustained drift.
    private const bool  LogEnabled       = true;
    private const bool  DebounceLog      = true;
    private const float LeanLogThreshold = 0.04f;  // m — below this the lean is noise
    private const float LeanLogCooldown  = 1.0f;   // s — min gap between sustained-lean reports
    private const float TiltWarnFraction = 0.75f;  // fraction of StumbleAngle that triggers warning

    private bool  _wasLeanActive  = false;
    private float _leanLogTimer   = 0f;
    private bool  _wasTiltWarning = false;

    private Dictionary<RigidBody3D, Vector3> _prevAngVel  = new();
    private Dictionary<RigidBody3D, float>   _jitterAccum = new();
    private int _jitterTick;

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

        // Yaw (joint X = world-up in anchor frame): no spring, no damping.
        // Yaw correction is applied via explicit ApplyTorque in ApplyBalance so the
        // sign convention is transparent and positive YawDamping always resists spin.
        _balanceJoint.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularLimit,  false);
        _balanceJoint.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularSpring, true);
        _balanceJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness,        0f);
        _balanceJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringDamping,          0f);
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

    /// <summary>
    /// Character's current right axis in world space, updated each physics tick as
    /// yaw input accumulates into _anchorRestBasis. Use this to orient width-wise
    /// visualisations (e.g. shoulder/hip crossbars) so they rotate with the character.
    /// </summary>
    public Vector3 AnchorRight => _anchorRestBasis.Y;

    /// <summary>
    /// Current movement-input direction in world space (XZ plane, unit length when input is
    /// held, Vector3.Zero when idle). Uses the same anchor-basis conversion as ApplyBalance so
    /// it rotates immediately with yaw input rather than waiting for physics to catch up.
    /// </summary>
    public Vector3 WorldInputDir
    {
        get
        {
            if (_rawInput.LengthSquared() <= 0.0001f) return Vector3.Zero;
            var anchorRight = _anchorRestBasis.Y;
            var rightFlat   = new Vector3(anchorRight.X, 0f, anchorRight.Z);
            if (rightFlat.LengthSquared() <= 0.01f)
                return new Vector3(-_rawInput.Y, 0f, _rawInput.X).Normalized();
            rightFlat    = rightFlat.Normalized();
            var fwdFlat  = rightFlat.Cross(Vector3.Up);
            var rightDir = fwdFlat.Cross(Vector3.Up);
            return (fwdFlat * (-_rawInput.Y) + rightDir * _rawInput.X).Normalized();
        }
    }

    /// <summary>
    /// Current lean angle (radians) in the movement direction. Zero when idle.
    /// Drive spine joint equilibria by a fraction of this to couple the upper body to balance.
    /// </summary>
    public float   LeanAngle { get; private set; }
    /// <summary>World-space XZ direction the body is leaning toward. Zero when idle.</summary>
    public Vector3 LeanDir   { get; private set; }

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

            if (LogEnabled)
            {
                var tiltWarn = tiltDeg > StumbleAngle * TiltWarnFraction;
                if (tiltWarn && !_wasTiltWarning)
                    GD.Print($"[Balance] Tilt warning  {tiltDeg:F1}° ({tiltDeg / StumbleAngle * 100f:F0}% of {StumbleAngle:F0}° limit)");
                else if (!tiltWarn && _wasTiltWarning)
                    GD.Print($"[Balance] Tilt recovered  {tiltDeg:F1}°");
                _wasTiltWarning = tiltWarn;
            }

            // Resolve input each tick from the torso's current physical orientation so
            // "forward" always means the direction the ragdoll is actually facing right now.
            Vector3 inputDir;
            if (_rawInput.LengthSquared() > 0.0001f)
            {
                // _anchorRestBasis.Y is the character's right axis (same convention as Basis.Y).
                // Using the anchor rather than the physical torso means the lean direction
                // updates immediately when a rotate key is held, not after physics lag.
                var anchorRight = _anchorRestBasis.Y;
                var rightFlat   = new Vector3(anchorRight.X, 0f, anchorRight.Z);
                if (rightFlat.LengthSquared() > 0.01f)
                {
                    rightFlat     = rightFlat.Normalized();
                    var fwdFlat   = rightFlat.Cross(Vector3.Up);
                    var rightDir  = fwdFlat.Cross(Vector3.Up);
                    inputDir      = fwdFlat * (-_rawInput.Y) + rightDir * _rawInput.X;
                }
                else
                {
                    // Torso is nearly horizontal (falling) — world fallback.
                    inputDir = new Vector3(-_rawInput.Y, 0f, _rawInput.X);
                }
            }
            else
            {
                inputDir = Vector3.Zero;
            }

            Basis targetBasis;
            if (inputDir.LengthSquared() > 0.0001f)
            {
                var leanDir   = inputDir.Normalized();
                var leanAxis  = Vector3.Up.Cross(leanDir).Normalized();
                var leanAngle = Mathf.Min(_rawInput.Length() * VelocityLean, Mathf.DegToRad(45f));
                targetBasis   = new Basis(leanAxis, leanAngle) * _anchorRestBasis;

                LeanAngle = leanAngle;
                LeanDir   = leanDir;

                // Directional force — lean alone is slow, force provides initial momentum.
                _lTorso.ApplyCentralForce(inputDir * MoveForce);
            }
            else
            {
                targetBasis = _anchorRestBasis;
                LeanAngle   = 0f;
                LeanDir     = Vector3.Zero;
                // Brake horizontal drift when idle.
                // Proportional to XZ velocity so it fades as the body stops — no jerk.
                if (IdleBrakingForce > 0f)
                {
                    var hVel = new Vector3(_lTorso.LinearVelocity.X, 0f, _lTorso.LinearVelocity.Z);
                    _lTorso.ApplyCentralForce(-hVel * IdleBrakingForce);
                }
            }

            // CoM-to-support-centre restoring force (ankle + hip strategy).
            // leanErr is computed unconditionally so reactive logging works even when
            // LeanRestoreForce = 0. Force is only applied when the parameter is nonzero.
            var leanErr      = Vector3.Zero;
            var hasSupportPt = false;
            if (_footStepper != null)
            {
                var supportCenter = _footStepper.GetSupportCenter();
                if (supportCenter.HasValue)
                {
                    var comXZ    = new Vector3(comPos.X, 0f, comPos.Z);
                    var centerXZ = new Vector3(supportCenter.Value.X, 0f, supportCenter.Value.Z);
                    leanErr      = comXZ - centerXZ;
                    hasSupportPt = true;

                    if (LeanRestoreForce > 0f)
                    {
                        var comVelXZ = new Vector3(comVel.X, 0f, comVel.Z);
                        _lTorso.ApplyCentralForce(-leanErr * LeanRestoreForce - comVelXZ * LeanRestoreDamping);
                    }
                }
            }

            // Reactive lean logging.
            if (LogEnabled && hasSupportPt)
            {
                var leanMag    = leanErr.Length();
                var leanActive = leanMag > LeanLogThreshold;
                _leanLogTimer  = Mathf.Max(0f, _leanLogTimer - (float)delta);

                // input arrow: X=strafe, negate Y because -rawInput.Y = forward in our convention
                var inArrow  = DirectionArrow(_rawInput.X, -_rawInput.Y);
                var velArrow = VelocityArrow(comVel, out var speed);
                var speedStr = $"{speed:F1}m/s";

                if (leanActive && !_wasLeanActive)
                {
                    // Rising edge — log immediately.
                    var forceStr = LeanRestoreForce > 0f
                        ? $"  restore={(-leanErr * LeanRestoreForce).Length():F1}N"
                        : "  (no restore force)";
                    GD.Print($"[Balance] Lean engaged  {leanMag:F2}m  in:{inArrow}  vel:{velArrow}{speedStr}{forceStr}");
                    _leanLogTimer = LeanLogCooldown;
                }
                else if (leanActive && (!DebounceLog || _leanLogTimer <= 0f))
                {
                    // Sustained — debounced repeat.
                    GD.Print($"[Balance] Lean sustained  {leanMag:F2}m  in:{inArrow}  vel:{velArrow}{speedStr}");
                    _leanLogTimer = LeanLogCooldown;
                }
                else if (!leanActive && _wasLeanActive)
                {
                    GD.Print($"[Balance] Lean settled  in:{inArrow}  vel:{velArrow}{speedStr}");
                }

                _wasLeanActive = leanActive;
            }

            _anchor.GlobalTransform = new Transform3D(targetBasis, _anchor.GlobalPosition);

            // Unified yaw velocity controller.
            // desiredYawVel drives rotation when input is held; at idle _rotateDir=0
            // so desiredYawVel=0 and the term reduces to plain anti-spin damping.
            // YawDamping is the controller gain (N·m per rad/s of error).
            if (YawDamping != 0f || _rotateDir != 0f)
            {
                var desiredYawVel = _rotateDir * Mathf.DegToRad(TurnMaxSpeed);
                var yawVel        = _lTorso.AngularVelocity.Dot(Vector3.Up);
                _lTorso.ApplyTorque(Vector3.Up * (YawDamping * (desiredYawVel - yawVel)));
            }

            // Accumulate the turn into _anchorRestBasis so the balance-spring upright
            // reference and foot-target forward direction both rotate with the character.
            if (_rotateDir != 0f)
                _anchorRestBasis = new Basis(Vector3.Up, _rotateDir * Mathf.DegToRad(TurnMaxSpeed) * (float)delta)
                                 * _anchorRestBasis;
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
    }

    private void Collapse()
    {
        var tiltDeg = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(
            _lTorso.GlobalTransform.Basis.X.Dot(Vector3.Up), -1f, 1f)));
        GD.Print($"[Balance] COLLAPSED  tilt={tiltDeg:F1}°  linVel={_lTorso.LinearVelocity:F2}");
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

    public void SetInputDir(Vector2 rawInput, float rotate = 0f)
    {
        _rawInput  = rawInput;
        _rotateDir = rotate;
    }

    public void OnJump() { }
    public void StandUp() { }

    // ── Log helpers ───────────────────────────────────────────────────────────

    /// Maps a 2D XY direction to a unicode arrow (8-way). Returns · when below threshold.
    private static string DirectionArrow(float x, float y)
    {
        if (x * x + y * y < 0.05f) return "·";
        int oct = ((int)Mathf.Round(Mathf.Atan2(y, x) * 4f / Mathf.Pi) % 8 + 8) % 8;
        return oct switch { 0 => "→", 1 => "↗", 2 => "↑", 3 => "↖",
                            4 => "←", 5 => "↙", 6 => "↓", 7 => "↘", _ => "·" };
    }

    /// Projects world-space XZ velocity onto character-local forward/right axes and
    /// returns a unicode arrow. Reads from _anchorRestBasis so it matches the facing used
    /// by the balance spring — same reference the input direction is resolved against.
    private string VelocityArrow(Vector3 worldVel, out float speed)
    {
        var velXZ     = new Vector3(worldVel.X, 0f, worldVel.Z);
        speed         = velXZ.Length();
        var rightRaw  = _anchorRestBasis.Y;
        var rightFlat = new Vector3(rightRaw.X, 0f, rightRaw.Z);
        if (rightFlat.LengthSquared() < 0.01f) return "·";
        rightFlat     = rightFlat.Normalized();
        var fwdFlat   = rightFlat.Cross(Vector3.Up);
        // Actual character right = fwdFlat.Cross(Up) = -rightFlat (vector triple product).
        // Negate the right component so the arrow matches the strafe convention.
        return DirectionArrow(-velXZ.Dot(rightFlat), velXZ.Dot(fwdFlat));
    }
}
