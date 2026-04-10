using Godot;

namespace godototype.objects.player.ragdoll;

/// <summary>
/// Saveable resource that drives all tunable behaviour on a RagdollCharacter.
/// Create presets in the Godot editor (e.g. standard.tres, drunk.tres) and
/// assign them to the Settings slot on the character node.
/// Leaving Settings null uses the default values defined here.
/// </summary>
[GlobalClass]
public partial class RagdollSettings : Resource
{
    // ── General ───────────────────────────────────────────────────────────────

    [Export] public float JumpForce { get; set; } = 15f;

    // ── Joints ────────────────────────────────────────────────────────────────
    [ExportGroup("Joints")]

    // Stiffness on torso (spine) joints. Higher = spine stays straighter.
    [Export] public float SpineStiffness { get; set; } = 6f;

    // Body-level angular damping on the torso segments. Applied directly by the physics
    // engine before constraint solving — more effective than ApplyTorque for killing
    // spin impulses. Kills the spawn yaw impulse without fighting joint constraints.
    [Export] public float TorsoAngularDamp { get; set; } = 8f;

    // Body-level angular damping on arm bodies (UArm, LArm, Hand).
    // The spinning torso drags arms via joint damping → arms oscillate.
    // High body-level damp kills this without any spring involved.
    [Export] public float ArmAngularDamp { get; set; } = 15f;

    // Body-level angular damping on the skull.
    [Export] public float HeadAngularDamp { get; set; } = 10f;

    // Spring stiffness on leg joints (hip, knee, ankle). How well the leg follows the IK target.
    [Export] public float LegStiffness { get; set; } = 10f;

    // Oscillation damping on leg joints.
    [Export] public float LegDamping { get; set; } = 1f;

    // Oscillation damping on torso/hip/shoulder joints.
    [Export] public float BodyDamping { get; set; } = 1f;

    // Stiffness on arm joints (shoulders, elbows, wrists).
    // Keep at 0 to prevent spring oscillation propagating down the arm chain.
    // Raise only if you need arms to hold a pose — any value > 0 risks hand shake.
    [Export] public float ArmStiffness { get; set; } = 0f;

    // Damping on arm joints. Resists motion without pulling toward any pose.
    [Export] public float ArmDamping { get; set; } = 3f;

    // Damping on wrists and ankles only.
    [Export] public float HandFootDamping { get; set; } = 1f;

    // How strongly the skull holds itself upright. Applied as a direct balance torque
    // on the skull body — same mechanism as the torso upright spring, just for the head.
    // 0 = head flops freely. Raise until it sits upright without snapping.
    [Export] public float HeadStiffness { get; set; } = 8f;

    // Damping on the skull's upright torque. Kills head bobbing/oscillation.
    [Export] public float HeadDamping { get; set; } = 5f;

    // Stiffness applied when Ctrl is held (T-pose snap). All joints spring toward
    // their rest pose. Balance and IK are suspended while this is active.
    [Export] public float SnapStiffness { get; set; } = 20f;

    // ── Balance ───────────────────────────────────────────────────────────────
    [ExportGroup("Balance")]

    // How strongly the body is pulled upright (PD spring stiffness).
    // Too low = tips over. Too high = jerky snap-back oscillation.
    [Export] public float UprightStiffness { get; set; } = 30f;

    // How quickly upright oscillation is damped out (PD spring damping).
    // Raise if the body rocks back and forth after a correction.
    [Export] public float UprightDamping { get; set; } = 10f;

    // Resistance to unintended yaw (vertical-axis) spin caused by asymmetric step forces.
    // Applied as explicit torque: torque = -YawDamping × yaw_angular_velocity.
    //   Positive = resists spin in whichever direction it's drifting (normal use).
    //   Negative = assists spin — use if stepping circles the wrong way and you need
    //              to push back. Start at 0, raise in small steps (try 2–8) until
    //              the straight-ahead shuffle stops curving.
    [Export] public float YawDamping { get; set; } = 2f;

    // How much the spine joints follow the balance lean direction.
    // 0 = rigid column, 1 = spine fully matches the lean angle.
    // Distributed evenly across all spine joints so total lean = LeanAngle × this value.
    [Export] public float SpineLeanFactor { get; set; } = 0.4f;

    // Tilt angle (degrees) at which the character gives up and ragdolls.
    [Export] public float StumbleAngle { get; set; } = 55f;

    // Horizontal force applied in the input direction while moving.
    [Export] public float MoveForce { get; set; } = 5f;

    // How far the body leans in the input direction (radians of lean per unit of input, 0–1).
    // The balance spring fights the lean, tipping the CoM and causing the character to stumble
    // toward the target. Try 0.2–0.5 for visible locomotion; beyond ~0.6 it falls over freely.
    [Export] public float VelocityLean { get; set; } = 0.3f;

    // Viscous drag (N·s/m) applied to the lower torso's XZ velocity when there is no input.
    // Prevents tiny spawn-settle velocities from drifting the CoM and triggering spurious steps.
    // Does not apply once input is held, so walking force is unaffected.
    [Export] public float IdleBrakingForce { get; set; } = 20f;

    // Drag on velocity perpendicular to the input direction while moving.
    // Counteracts lateral drift from asymmetric step reaction forces.
    // Start at 15; raise if the body still curves; lower if movement feels sluggish turning.
    [Export] public float LateralDampForce { get; set; } = 15f;

    // PD spring pulling the whole-body CoM back toward the planted-foot midpoint.
    // Implements the ankle + hip strategy — resists idle lean and position drift without
    // requiring a step. The balance joint corrects tilt (orientation); this corrects drift
    // (position). Start at 0 and raise in steps: try 10 → 20 → 40.
    // Too high = jerky snap-back or fights walking; too low = drift persists.
    [Export] public float LeanRestoreForce   { get; set; } = 15f;

    // Damping on the lean-restore spring. Prevents the CoM from oscillating around the
    // support centre after a correction. Raise if you see the body rock back and forth.
    // Typical range: 5–15. Should be roughly LeanRestoreForce / 3.
    [Export] public float LeanRestoreDamping { get; set; } = 5f;


    // ── Stepping ──────────────────────────────────────────────────────────────
    [ExportGroup("Stepping")]

    // How far the foot must drift from its ideal position before a new step fires (metres).
    // This directly controls step frequency and stride length — smaller = steps fire sooner
    // and more often, larger = feet lag further before correcting.
    [Export] public float StepTriggerDistance { get; set; } = 0.25f;

    // How far ahead of the hip the foot targets in the movement direction.
    [Export] public float StanceFwd { get; set; } = 0.0f;

    // Controls the capture-point foot placement (Linear Inverted Pendulum Model).
    // Foot target = CoM_position + CoM_velocity × CaptureGain, keeping each foot
    // laterally offset by its hip position so stance width is preserved naturally.
    // This drives automatic balance recovery — when the body decelerates or stops leaning,
    // feet step back under the CoM without any input from the player.
    //   ~0.25 = physically accurate for a ~0.6 m hip height (ω = sqrt(g/h) ≈ 4.0).
    //   Lower  = sluggish recovery, character may still topple when stopping.
    //   Higher = aggressive stepping, feet overshoot — gives an inebriated stagger at large values.
    [Export] public float CaptureGain { get; set; } = 0.25f;

    // XZ speed (m/s) below which the capture-point look-ahead is disabled. When nearly at
    // rest, even tiny settle velocities would project the foot target slightly forward and
    // trigger steps; this threshold keeps feet planted until the body is genuinely moving.
    [Export] public float RestVelocityThreshold { get; set; } = 0.1f;

    // Upward impulse applied to the torso each time a foot plants (N·s).
    // Creates the vertical body bob characteristic of natural walking rhythm.
    //   0    = no bounce, torso height is purely physics-driven.
    //   0.5  = subtle bob, good for normal walking.
    //   2+   = exaggerated bounce, useful for heavy/drunk characters.
    // Too high = character hops visibly off the ground each step.
    [Export] public float StepBounce { get; set; } = 0f;

    // Upward force on the upper leg during swing — lifts the foot off the ground.
    [Export] public float LegLiftForce { get; set; } = 8f;

    // Horizontal drive force on the upper leg toward the step target.
    [Export] public float LegDriveForce { get; set; } = 40f;

    // Damping on the upper leg's horizontal velocity during swing.
    [Export] public float LegDriveDamp { get; set; } = 4f;

    // Fraction (0–1) of the yaw torque injected by step drive forces to cancel on the torso.
    // 0 = no cancellation (old behaviour). Start at 0.5 and raise if straight walking
    // still drifts even after reducing LateralDampForce; back off if the body over-corrects.
    [Export] public float StepYawCancel { get; set; } = 0.5f;

    // Fine spring force applied directly to the foot toward the exact target.
    [Export] public float FootSpringForce { get; set; } = 120f;

    // Damping on the foot's velocity during swing.
    [Export] public float FootSpringDamp { get; set; } = 10f;

    // How close the swinging foot must get to its target before it plants (metres).
    // Should be smaller than StepTriggerDistance or the foot plants before it arrives.
    [Export] public float PlantTolerance { get; set; } = 0.10f;

    // Minimum time between steps on the same foot.
    [Export] public float StepCooldown { get; set; } = 0.20f;

    // ── Rotation ──────────────────────────────────────────────────────────────
    [ExportGroup("Rotation")]

    // Maximum yaw rate while a rotate input is held (degrees/second).
    // Higher = snappier turns. At 90 °/s a quarter-turn takes ~1 second.
    // Combine with YawDamping (under Balance) — that value is the controller
    // gain: torque = YawDamping × (desiredYawVel − currentYawVel).
    [Export] public float TurnMaxSpeed { get; set; } = 90f;

    // ── Crouch ────────────────────────────────────────────────────────────────
    [ExportGroup("Crouch")]

    // Spring stiffness applied to the entire upper body (torso, neck, shoulders) while crouching.
    // Keeps the spine column stacked and head aligned over the CoM as the hips drop.
    // Set higher than SpineStiffness to resist forward slump; lower for a more relaxed squat.
    [Export] public float CrouchBodyStiffness { get; set; } = 20f;

    // Target knee bend angle when crouching (degrees, positive = forward bend).
    // Practical range: 10°–80°. Higher = deeper squat. The knee spring drives toward
    // this angle; CrouchKneeStiffness controls how snappily it gets there.
    [Export] public float CrouchKneeAngle { get; set; } = 40f;


    // Hip joint flex angle when crouching (degrees, positive = forward flex same as knee convention).
    // The hip and knee together geometrically lower the CoM — no downward force needed.
    // Start around half of CrouchKneeAngle and tune from there.
    [Export] public float CrouchHipAngle { get; set; } = 20f;

    // Ankle compensation angle when crouching (degrees).
    // Works through two mechanisms depending on foot state:
    //
    //   Planted foot: the foot is spring-held in place, so ankle torque acts on the SHIN
    //   instead — rotating it backward and pulling the knee back over the foot. This is
    //   the primary defence against the forward body tip that bent knees create, because
    //   it geometrically recentres the knee mass over the support polygon.
    //
    //   Swinging foot: affects where the foot lands — dorsiflexion makes the step target
    //   land further back, preventing micro-steps from forward shin lean.
    //
    // Use a NEGATIVE value (dorsiflexion, opposite to CrouchKneeAngle's sign).
    // Start at roughly -(CrouchKneeAngle / 2) and tune from there.
    // Too much (too negative) = shin rocks backward and foot lifts; too little = forward tip persists.
    // LeanRestoreForce (above) is the primary position correction; this is the geometric complement.
    [Export] public float CrouchAnkleAngle { get; set; } = 0f;
}
