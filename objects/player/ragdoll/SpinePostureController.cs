using Godot;
using godototype.world;

namespace godototype.objects.player.ragdoll;

/// <summary>
/// Applies world-space PD (Proportional-Derivative) torques to the spine segments each
/// physics tick to maintain upright posture and level shoulders.
///
/// This is the active-ragdoll "muscle" pattern used by Euphoria / NaturalMotion:
/// each body computes a desired orientation and drives itself there with a torque
/// proportional to the angular error, minus a damping term on angular velocity.
///
///   torque = Strength × angularError − Damping × angularVelocity
///
/// Two behaviours are layered:
///
///   1. Segment stacking — mTorso and uTorso each track the segment below them.
///      The spine behaves like a flexible rod: balance lean on lTorso naturally
///      propagates upward through all segments without per-segment tuning.
///
///   2. Shoulder leveling — an additional roll-axis torque on uTorso zeroes the
///      sideways tilt of the shoulder girdle. The shoulder pole in the debug overlay
///      should stay horizontal when this is working correctly.
///
/// This controller is disabled during full ragdoll (balance collapsed) so the character
/// goes properly limp rather than fighting the posture spring while falling.
/// </summary>
public partial class SpinePostureController : Node
{
    private RigidBody3D       _lTorso;
    private RigidBody3D       _mTorso;
    private RigidBody3D       _uTorso;
    private BalanceController _balance;
    private bool              _active;

    // Stored to guarantee delegate identity on unregister — List.Remove uses Equals,
    // which is target+method for delegates, but storing explicitly is unambiguous.
    private System.Action<double> _tickCallback;

    // Tunable parameters — fed in from RagdollSettings by RagdollCharacter.
    private float _postureStrength;
    private float _postureDamping;
    private float _shoulderLevelStrength;
    private float _shoulderLevelDamping;

    /// <summary>
    /// Wire up references and register with PoseManager.
    /// Must be called after BalanceController.Init so AnchorRight is available.
    /// </summary>
    public void Init(
        RigidBody3D lTorso, RigidBody3D mTorso, RigidBody3D uTorso,
        BalanceController balance,
        float postureStrength,       float postureDamping,
        float shoulderLevelStrength, float shoulderLevelDamping)
    {
        _lTorso                = lTorso;
        _mTorso                = mTorso;
        _uTorso                = uTorso;
        _balance               = balance;
        _postureStrength       = postureStrength;
        _postureDamping        = postureDamping;
        _shoulderLevelStrength = shoulderLevelStrength;
        _shoulderLevelDamping  = shoulderLevelDamping;
        _active                = true;

        _tickCallback = Tick;
        PoseManager.Register(_tickCallback);
    }

    public override void _ExitTree() => PoseManager.Unregister(_tickCallback);

    /// <summary>
    /// Enable or disable posture correction. Mirror the BalanceController enable/disable
    /// calls in RagdollCharacter so both systems are always in the same state.
    /// </summary>
    public void SetActive(bool active) => _active = active;

    private void Tick(double _delta)
    {
        if (!_active) return;

        // Bail out when the balance has collapsed — the character is falling and should be
        // fully limp. Continuing to apply posture torques would fight the ragdoll and looks wrong.
        if (_balance == null || _balance.State != BalanceController.BalanceState.Standing) return;

        if (!IsInstanceValid(_lTorso) || !IsInstanceValid(_mTorso) || !IsInstanceValid(_uTorso)) return;

        // ── Segment stacking ──────────────────────────────────────────────────────────
        // Each segment targets the orientation of the one below. lTorso is anchored by
        // BalanceController's upright spring; mTorso and uTorso follow it up the chain.

        // Lower spine: mTorso tracks lTorso.
        ApplyPDTorque(_mTorso, _lTorso.GlobalBasis, _postureStrength, _postureDamping);

        // Upper spine: uTorso tracks mTorso.
        // Note: uTorso reads the *current* mTorso orientation, not the target — this means
        // corrections propagate with a one-tick lag up the chain, which is intentional.
        // Targeting the post-correction mTorso would cause oscillation.
        ApplyPDTorque(_uTorso, _mTorso.GlobalBasis, _postureStrength, _postureDamping);

        // ── Shoulder leveling ─────────────────────────────────────────────────────────
        // Stacking handles pitch (forward/back) and yaw; this pass handles roll (tilt).
        // Applied to uTorso only — the shoulder girdle is what we're leveling.
        ApplyShoulderLevelTorque();
    }

    /// <summary>
    /// Euphoria-style PD torque controller.
    /// Extracts the rotation error as axis × angle (Atan2 form for numerical stability),
    /// then applies: torque = Strength × (axis × angle) − Damping × angularVelocity.
    ///
    /// The full angular-velocity damping is intentional: each body should track smoothly,
    /// not oscillate. This stacks with the body-level TorsoAngularDamp — keep PostureDamping
    /// low (1–4) so the two don't over-damp the spine and kill the sense of physical weight.
    /// </summary>
    private static void ApplyPDTorque(RigidBody3D body, Basis desiredBasis, float strength, float damping)
    {
        var currentQ = body.GlobalBasis.GetRotationQuaternion();
        var desiredQ = desiredBasis.GetRotationQuaternion();

        // errorQ = the rotation that takes current orientation to desired orientation.
        var errorQ = desiredQ * currentQ.Inverse();

        var xyz    = new Vector3(errorQ.X, errorQ.Y, errorQ.Z);
        var xyzLen = xyz.Length();
        if (xyzLen < 1e-6f) return; // already at target — skip to avoid divide-by-zero

        var axis = xyz / xyzLen;

        // Atan2 gives angle in (−π, π], more numerically stable than Acos near 0 or ±π.
        // Multiplying by 2 converts from half-angle (quaternion storage) to full rotation angle.
        var angle = 2f * Mathf.Atan2(xyzLen, errorQ.W);

        body.ApplyTorque(axis * (angle * strength) - body.AngularVelocity * damping);
    }

    /// <summary>
    /// Applies a roll-only corrective torque to the upper torso so the shoulder girdle
    /// stays level (no sideways tilt visible on the violet debug pole crossbar).
    ///
    /// In this rig Basis.X is the torso's local up axis. Roll error = how much Basis.X
    /// has tilted sideways into the character-right direction. The corrective torque
    /// rotates around the character-forward axis to bring the roll back to zero.
    ///
    ///   rollErr     = torsoUp · charRight       (0 when level)
    ///   corrective  = charFwd × (rollErr × K − ω_fwd × D)
    ///
    /// The stacking spring already handles pitch and yaw — this runs after it and
    /// only adds force around the forward axis, leaving the other axes undisturbed.
    /// </summary>
    private void ApplyShoulderLevelTorque()
    {
        var charRight = _balance.AnchorRight;
        var rightFlat = new Vector3(charRight.X, 0f, charRight.Z);
        if (rightFlat.LengthSquared() < 0.01f) return; // nearly horizontal — no valid forward axis

        rightFlat = rightFlat.Normalized();

        // Forward axis — same cross-product convention used throughout BalanceController.
        var charFwd = rightFlat.Cross(Vector3.Up);

        // Basis.X = local up in the torso orientation convention.
        var torsoUp = _uTorso.GlobalBasis.X;

        // How much the torso's up vector has tilted toward charRight.
        // Positive → tilted right, negative → tilted left.
        var rollErr = torsoUp.Dot(rightFlat);

        // Angular velocity component around the forward axis (the one we're correcting).
        var rollDampVel = _uTorso.AngularVelocity.Dot(charFwd);

        // Torque around charFwd: positive rollErr needs positive (CCW) rotation to correct.
        _uTorso.ApplyTorque(charFwd * (rollErr * _shoulderLevelStrength - rollDampVel * _shoulderLevelDamping));
    }
}
