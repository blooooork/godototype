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

    private const bool LogEnabled = false;

    private readonly Generic6DofJoint3D _hipJoint;
    private readonly Generic6DofJoint3D _kneeJoint;
    private readonly Generic6DofJoint3D _ankleJoint;
    private readonly RayCast3D          _groundRay;

    private float _upperLen;
    private float _lowerLen;
    private Basis _restHipBasis;
    private bool  _initialised;
    private bool  _active = true;
    private int   _logTick;

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

        // Keep the foot target directly below the hip at full leg extension.
        // This ensures the IK always targets a straight leg regardless of how much
        // the hip has dropped — the hip-to-target distance stays at total leg length,
        // so flex ≈ 0 and the spring pushes the foot into the floor to support the body.
        FootTarget.GlobalPosition = new Vector3(
            hipPos.X,
            hipPos.Y - (_upperLen + _lowerLen - 0.001f),
            hipPos.Z
        );

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