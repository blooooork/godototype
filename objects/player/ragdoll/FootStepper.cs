using Godot;

namespace godototype.objects.player.ragdoll;

/// <summary>
/// Drives foot placement each physics tick.
///
/// Swing strategy:
///   1. Zero the leg joint stiffness so the leg swings freely — no fighting the passive spring.
///   2. Apply upward lift + horizontal drive to the upper leg (the pivot that swings the chain).
///   3. Apply a fine spring to the foot for exact target placement.
///   4. On plant: restore joint stiffness so the stance spring holds the foot in position.
///
/// Step targets are computed in CHARACTER-LOCAL frame (torso basis), never raw world X/Z.
/// One foot swings at a time.
/// </summary>
public partial class FootStepper : Node
{
    // ── Tuning ───────────────────────────────────────────────────────────────

    public float StepTriggerDistance { get; set; } = 0.25f;
    public float StanceFwd           { get; set; } = 0.0f;
    public float CaptureGain         { get; set; } = 0.25f;
    public float LegLiftForce   { get; set; } = 8f;
    public float LegDriveForce  { get; set; } = 40f;
    public float LegDriveDamp   { get; set; } = 4f;
    public float FootSpringForce { get; set; } = 120f;
    public float FootSpringDamp  { get; set; } = 10f;
    public float PlantTolerance { get; set; } = 0.10f;
    public float StepCooldown   { get; set; } = 0.20f;

    // ── Internal ─────────────────────────────────────────────────────────────

    private enum FootState { Planted, Swinging }

    private struct Foot
    {
        public RigidBody3D         Body;      // foot sphere — fine spring target
        public RigidBody3D         ULeg;      // upper leg — lift + horizontal drive
        public RigidBody3D         HipBody;   // hip joint body — tracks hip socket position
        public Generic6DofJoint3D  HipJoint;
        public Generic6DofJoint3D  KneeJoint;
        public Generic6DofJoint3D  AnkleJoint;
        public FootState           State;
        public Vector3             Target;
        public float               Cooldown;
    }

    private Foot[]      _feet       = new Foot[2];
    private RigidBody3D _lTorso;
    private float       _legStiffness;
    private bool        _enabled    = true;

    private const int L = 0, R = 1;

    // Read by debug overlay.
    public Vector3 LeftTarget  => _feet[L].Target;
    public Vector3 RightTarget => _feet[R].Target;
    public bool    LeftSwing   => _feet[L].State == FootState.Swinging;
    public bool    RightSwing  => _feet[R].State == FootState.Swinging;
    public bool    IsReady     => _lTorso != null;

    public void Enable()
    {
        _enabled = true;
        for (int i = 0; i < 2; i++)
            SetLegJointStiffness(ref _feet[i], _legStiffness);
    }

    public void Disable()
    {
        _enabled = false;
        // Zero leg joint stiffness so legs go limp — they won't fight gravity during a fall.
        for (int i = 0; i < 2; i++)
            SetLegJointStiffness(ref _feet[i], 0f);
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void Setup(
        RigidBody3D lTorso,
        RigidBody3D lFoot,       RigidBody3D rFoot,
        RigidBody3D lULeg,       RigidBody3D rULeg,
        RigidBody3D lHip,        RigidBody3D rHip,
        Generic6DofJoint3D lHipJoint,   Generic6DofJoint3D rHipJoint,
        Generic6DofJoint3D lKneeJoint,  Generic6DofJoint3D rKneeJoint,
        Generic6DofJoint3D lAnkleJoint, Generic6DofJoint3D rAnkleJoint,
        float legStiffness)
    {
        _lTorso       = lTorso;
        _legStiffness = legStiffness;

        _feet[L] = new Foot
        {
            Body = lFoot, ULeg = lULeg, HipBody = lHip,
            HipJoint = lHipJoint, KneeJoint = lKneeJoint, AnkleJoint = lAnkleJoint,
            State = FootState.Planted, Target = lFoot.GlobalPosition,
        };
        _feet[R] = new Foot
        {
            Body = rFoot, ULeg = rULeg, HipBody = rHip,
            HipJoint = rHipJoint, KneeJoint = rKneeJoint, AnkleJoint = rAnkleJoint,
            State = FootState.Planted, Target = rFoot.GlobalPosition,
        };
    }

    // ── Physics tick ─────────────────────────────────────────────────────────

    public override void _PhysicsProcess(double delta)
    {
        if (!_enabled || _lTorso == null || !GodotObject.IsInstanceValid(_lTorso)) return;

        var dt = (float)delta;

        var rawFwd  = _lTorso.GlobalTransform.Basis.Y;
        var fwdFlat = new Vector3(rawFwd.X, 0f, rawFwd.Z);
        if (fwdFlat.LengthSquared() < 0.01f) fwdFlat = Vector3.Forward;
        fwdFlat = fwdFlat.Normalized();

        var groundY = ComputeGroundY();

        // Capture point — where the foot must land for the body to reach equilibrium.
        // Based on the Linear Inverted Pendulum Model: x_capture = x_com + v_com × gain.
        // Using full XZ velocity (not just forward) means the foot automatically steps
        // back under the body when decelerating, with no input required.
        // CaptureGain ≈ 1/ω where ω = sqrt(g/h): physically correct at ~0.25 for 0.6m
        // hip height. Higher values step further ahead — large values give inebriated overshoot.
        var comXZ      = new Vector3(_lTorso.GlobalPosition.X,   groundY, _lTorso.GlobalPosition.Z);
        var comVelXZ   = new Vector3(_lTorso.LinearVelocity.X,   0f,      _lTorso.LinearVelocity.Z);
        var captureXZ  = comXZ + comVelXZ * CaptureGain;

        for (int i = 0; i < 2; i++)
        {
            ref var f = ref _feet[i];
            if (!GodotObject.IsInstanceValid(f.Body) ||
                !GodotObject.IsInstanceValid(f.ULeg) ||
                !GodotObject.IsInstanceValid(f.HipBody)) continue;

            f.Cooldown = Mathf.Max(0f, f.Cooldown - dt);

            // Lateral offset keeps this foot at the correct width relative to CoM.
            // Using hip displacement from CoM preserves natural foot spread without a separate parameter.
            var hipXZ         = new Vector3(f.HipBody.GlobalPosition.X, groundY, f.HipBody.GlobalPosition.Z);
            var lateralOffset = hipXZ - comXZ;
            var idealPos      = captureXZ + lateralOffset + fwdFlat * StanceFwd;
            idealPos.Y        = groundY;

            if (f.State == FootState.Planted)
            {
                bool  otherPlanted = _feet[1 - i].State == FootState.Planted;
                float drift        = (f.Body.GlobalPosition - idealPos).Length();

                if (f.Cooldown <= 0f && otherPlanted && drift > StepTriggerDistance)
                {
                    f.Target = idealPos;
                    f.State  = FootState.Swinging;
                    SetLegJointStiffness(ref f, 0f);   // free the leg to swing
                }
            }
            else // Swinging
            {
                // ── Upper leg: lift + horizontal drive ───────────────────────
                // With joint stiffness zeroed the leg hangs free — force moves it
                // without fighting the passive spring.
                var ulegXZ      = new Vector3(f.ULeg.GlobalPosition.X, 0f, f.ULeg.GlobalPosition.Z);
                var targetXZ    = new Vector3(f.Target.X,              0f, f.Target.Z);
                var horizErr    = targetXZ - ulegXZ;
                var horizVel    = new Vector3(f.ULeg.LinearVelocity.X, 0f, f.ULeg.LinearVelocity.Z);
                f.ULeg.ApplyCentralForce(
                    horizErr * LegDriveForce
                    + Vector3.Up * LegLiftForce
                    - horizVel  * LegDriveDamp);

                // ── Foot: fine spring to exact target ────────────────────────
                var err = f.Target - f.Body.GlobalPosition;
                f.Body.ApplyCentralForce(err * FootSpringForce - f.Body.LinearVelocity * FootSpringDamp);

                if (err.Length() < PlantTolerance)
                {
                    f.State    = FootState.Planted;
                    f.Cooldown = StepCooldown;
                    SetLegJointStiffness(ref f, _legStiffness);  // restore stance spring
                }
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetLegJointStiffness(ref Foot f, float s)
    {
        SetJointStiffness(f.HipJoint,   s);
        SetJointStiffness(f.KneeJoint,  s);
        SetJointStiffness(f.AnkleJoint, s);
    }

    private static void SetJointStiffness(Generic6DofJoint3D j, float s)
    {
        if (!GodotObject.IsInstanceValid(j)) return;
        j.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, s);
        j.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, s);
        j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, s);
    }

    private float ComputeGroundY()
    {
        var minY = float.MaxValue;
        for (int i = 0; i < 2; i++)
        {
            if (!GodotObject.IsInstanceValid(_feet[i].Body)) continue;
            var y = _feet[i].Body.GlobalPosition.Y;
            if (y < minY) minY = y;
        }
        return minY < float.MaxValue ? minY : 0f;
    }
}
