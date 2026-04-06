using Godot;

namespace godototype.objects.player.ragdoll;

/// <summary>
/// Analytical 2-bone IK for a single leg. Plain C# — not a Node.
/// Owned and ticked by BalanceController each physics step.
/// Drives hip / knee / ankle Generic6DofJoint3D spring equilibria.
///
/// Hip joint axes (based on Leg.tscn orientation):
///   Joint X ≈ world +Z (char forward)  → rotation takes leg toward world +X (abduction)
///   Joint Z ≈ world −X (char left)     → rotation takes leg toward world +Z (flexion)
/// If the character flexes sideways instead of forward in-engine, swap the hipX / hipZ assignments.
/// </summary>
public class LegIK
{
    public bool   IsGrounded { get; private set; }
    public Node3D FootTarget { get; set; }

    // Label used in log output — set to "L" or "R" by caller.
    public string Label { get; set; } = "?";

    // Horizontal distance from directly-below-hip at which a step is triggered.
    public float StepThreshold { get; set; } = 0.2f;

    // How many physics ticks the swing phase lasts (foot lerps toward new position).
    public int StepSwingTicks { get; set; } = 15;

    // How many physics ticks to wait after planting before this leg can step again.
    public int StepCooldownTicks { get; set; } = 10;

    // Set false by BalanceController while the other leg is mid-swing.
    public bool CanStep { get; set; } = true;

    // True while this leg is in the swing / lerp phase.
    public bool IsStepping { get; private set; }

    private const bool LogEnabled = false;

    private readonly Generic6DofJoint3D _hipJoint;
    private readonly Generic6DofJoint3D _kneeJoint;
    private readonly Generic6DofJoint3D _ankleJoint;
    private readonly RayCast3D          _groundRay;

    private float   _upperLen;
    private float   _lowerLen;
    private Basis   _restHipBasis;
    private bool    _initialised;
    private bool    _active = true;
    private int     _logTick;
    private int     _swingTick;
    private int     _cooldownTick;
    private Vector3 _stepTarget;

    public LegIK(
        Generic6DofJoint3D hipJoint,
        Generic6DofJoint3D kneeJoint,
        Generic6DofJoint3D ankleJoint,
        RigidBody3D        foot,
        Node3D             footTarget)
    {
        _hipJoint   = hipJoint;
        _kneeJoint  = kneeJoint;
        _ankleJoint = ankleJoint;
        _groundRay  = foot.GetNodeOrNull<RayCast3D>("GroundRay");
        FootTarget  = footTarget;
    }

    /// <summary>
    /// Call once all bodies are in their spawn positions (deferred from _Ready).
    /// Records the joint rest frame and measures limb lengths from actual node positions.
    /// </summary>
    public void Init()
    {
        _restHipBasis = _hipJoint.GlobalTransform.Basis;
        _upperLen     = _hipJoint.GlobalPosition.DistanceTo(_kneeJoint.GlobalPosition);
        _lowerLen     = _kneeJoint.GlobalPosition.DistanceTo(_ankleJoint.GlobalPosition);
        _initialised  = true;

        GD.Print($"[LegIK:{Label}] Init — upperLen={_upperLen:F3} lowerLen={_lowerLen:F3} " +
                 $"maxReach={_upperLen + _lowerLen:F3} | " +
                 $"groundRay={((_groundRay != null) ? "found" : "MISSING")} | " +
                 $"footTarget={(FootTarget != null ? FootTarget.Name : "MISSING")}");
    }

    public void SetActive(bool active) => _active = active;

    public void Solve()
    {
        if (!_initialised) return;
        if (!GodotObject.IsInstanceValid(_hipJoint)) return;
        if (!_active) return;

        // Ground detection runs regardless of whether IK target is set
        IsGrounded = _groundRay?.IsColliding() ?? false;

        if (FootTarget == null) return;

        var hipPos = _hipJoint.GlobalPosition;

        // Track Y only — prevents hip-sag knee bend while keeping X/Z planted.
        var planted = FootTarget.GlobalPosition;
        planted.Y = hipPos.Y - (_upperLen + _lowerLen - 0.001f);

        if (IsStepping)
        {
            // Swing phase: lerp foot toward the step target over SwingTicks frames.
            // Lerp distributes the spring force over time instead of a violent snap.
            float t = 1f - (float)_swingTick / StepSwingTicks;   // 0→1 over swing duration
            planted.X = Mathf.Lerp(FootTarget.GlobalPosition.X, _stepTarget.X, t);
            planted.Z = Mathf.Lerp(FootTarget.GlobalPosition.Z, _stepTarget.Z, t);
            _swingTick--;
            if (_swingTick <= 0)
            {
                planted.X    = _stepTarget.X;
                planted.Z    = _stepTarget.Z;
                IsStepping   = false;
                _cooldownTick = StepCooldownTicks;
            }
        }
        else if (_cooldownTick > 0)
        {
            _cooldownTick--;
        }
        else if (CanStep)
        {
            var drift = new Vector2(planted.X - hipPos.X, planted.Z - hipPos.Z).Length();
            if (drift > StepThreshold)
            {
                _stepTarget = new Vector3(hipPos.X, planted.Y, hipPos.Z);
                IsStepping  = true;
                _swingTick  = StepSwingTicks;
            }
        }

        FootTarget.GlobalPosition = planted;

        var toTarget = FootTarget.GlobalPosition - hipPos;
        var rawDist  = toTarget.Length();
        var dist     = Mathf.Clamp(rawDist, 0.001f, _upperLen + _lowerLen - 0.001f);

        // ── Knee: law of cosines → flex angle ────────────────────────────────
        var cosKnee = (_upperLen * _upperLen + _lowerLen * _lowerLen - dist * dist)
                    / (2f * _upperLen * _lowerLen);
        cosKnee = Mathf.Clamp(cosKnee, -1f, 1f);
        var flex = Mathf.Pi - Mathf.Acos(cosKnee);   // 0 = straight, grows as dist shrinks

        // ── Hip: desired leg direction in joint rest frame ────────────────────
        var dir  = _restHipBasis.Inverse() * toTarget.Normalized();
        var hipX = Mathf.Atan2(-dir.Z, -dir.Y);
        var hipZ = Mathf.Atan2( dir.X, -dir.Y);

        // ── Ankle: keep foot roughly level ───────────────────────────────────
        var ankleCorr = Mathf.Clamp(flex * 0.6f, 0f, Mathf.DegToRad(30f));

        // ── Log every 30 ticks (~0.5 s at 60 hz physics) ─────────────────────
        if (LogEnabled && ++_logTick % 30 == 0)
        {
            var clamped = !Mathf.IsEqualApprox(rawDist, dist);
            GD.Print($"[LegIK:{Label}] grounded={IsGrounded} | " +
                     $"dist={dist:F3}{(clamped ? $" (raw={rawDist:F3} CLAMPED)" : "")} | " +
                     $"flex={Mathf.RadToDeg(flex):F1}° | " +
                     $"hipX={Mathf.RadToDeg(hipX):F1}° hipZ={Mathf.RadToDeg(hipZ):F1}° | " +
                     $"ankle={Mathf.RadToDeg(ankleCorr):F1}° | " +
                     $"target={FootTarget.GlobalPosition:F2} hip={hipPos:F2}");
        }

        // ── Write equilibria ──────────────────────────────────────────────────
        // Spring X range −130° → +5°: 0 = straight, negative = flex
        _kneeJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, -flex);
        _hipJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint,  hipX);
        _hipJoint.SetParamZ(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint,  hipZ);
        _ankleJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, ankleCorr);
    }
}