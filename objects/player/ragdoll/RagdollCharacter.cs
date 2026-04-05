using Godot;
using blendporter.definition;
using blendporter.service;
using godototype.constants;
using godototype.input;
using godototype.objects.player.ragdoll.limbs;
using System;
using System.Collections.Generic;
using godototype.camera;
using godototype.world;

namespace godototype.objects.player.ragdoll;

public partial class RagdollCharacter : Node3D, IResettable
{
    public enum BodyGroup
    {
        All,
        Parts, JointBodies,
        Torso,
        LeftArm, RightArm, Arms,
        LeftLeg, RightLeg, Legs,
    }

    [Export] public float JumpForce { get; set; } = 15f;

    // ── Joints ───────────────────────────────────────────────────────────────
    // Controls how joints behave: how stiff they are, how quickly oscillation dies.
    [ExportGroup("Joints")]

    // Tiny baseline spring on all non-IK joints except spine and arms.
    [Export] public float PassiveStiffness { get; set; } = 0.5f;

    // Stiffness on torso (spine) joints. Higher = spine stays straighter.
    // PassiveStiffness=0.5 lets the spine bend 35°+ which causes centrifugal lean.
    // Keep this much higher than PassiveStiffness — 5–10 is a good range.
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

    // Spring stiffness on IK-driven joints (legs). This is how well the leg follows
    // the IK target — NOT a T-pose snap. Keep moderate; physics still overrides hard impacts.
    [Export] public float IKStiffness { get; set; } = 10f;

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
    // Controls the active balancing system that keeps the character upright.
    [ExportGroup("Balance")]

    // How strongly the body is pulled upright (PD spring stiffness).
    // Too low = tips over. Too high = jerky snap-back oscillation.
    [Export] public float UprightStiffness { get; set; } = 30f;

    // How quickly upright oscillation is damped out (PD spring damping).
    // Raise if the body rocks back and forth after a correction.
    [Export] public float UprightDamping { get; set; } = 10f;

    // Resists spinning in place (yaw). Applied to all torso segments.
    [Export] public float YawDamping { get; set; } = 20f;

    // How much of the raw tilt error carries over each physics tick (0–1).
    // Lower = heavier smoothing, slower reaction. Higher = more responsive but more jitter.
    // At 60hz physics: 0.1 ≈ smoothed over ~10 frames, 0.5 ≈ ~2 frames.
    [Export] public float ErrorSmoothing { get; set; } = 0.25f;

    // Tilt angle (degrees) within which no correction force is applied.
    // Prevents micro-oscillation near-upright where the error flips sign every frame.
    [Export] public float TiltDeadzone { get; set; } = 1.5f;

    // Tilt angle (degrees) at which the character transitions upright → stumbling.
    [Export] public float StumbleAngle { get; set; } = 55f;

    // Horizontal force applied in the input direction while moving.
    [Export] public float MoveForce { get; set; } = 5f;

    // How much the body leans forward proportional to its horizontal speed.
    // Mimics natural gait lean — keep very small.
    [Export] public float VelocityLean { get; set; } = 0.005f;

    // Yaw torque applied when the player rotates.
    [Export] public float RotateTorque { get; set; } = 3f;

    // Upward impulse applied when recovering from a fallen state.
    [Export] public float RecoveryImpulse { get; set; } = 8f;

    private Dictionary<RigidBody3D, Transform3D> _restTransforms;
    private Dictionary<RigidBody3D, Transform3D> _restOffsets;

    private IVirtualCamera    _camera;
    private Node3D            _cameraNode;
    private CameraClaim       _cameraClaim;
    private BalanceController _balanceController;
    private LegIK             _leftLegIK;
    private LegIK             _rightLegIK;

    // Whether BalanceController + IK are actively running.
    // False = full ragdoll — physics only, no active systems.
    private bool _isActive = true;

    // Actions
    private Action<string> _onJump;
    private Action<string> _onCrouch;
    private Action<string> _onCrouchRelease;
    private Action<string> _onForward, _onBackward, _onStrafeLeft, _onStrafeRight, _onRotateLeft, _onRotateRight;

    // Joints managed by ragdoll toggle (everything except left leg)
    private Generic6DofJoint3D   _neckJoint;
    private Generic6DofJoint3D   _leftShoulder;
    private Generic6DofJoint3D   _rightShoulder;
    private Generic6DofJoint3D   _leftElbow;
    private Generic6DofJoint3D   _rightElbow;
    private Generic6DofJoint3D   _leftWrist;
    private Generic6DofJoint3D   _rightWrist;
    private Generic6DofJoint3D   _rightHip;
    private Generic6DofJoint3D   _rightKnee;
    private Generic6DofJoint3D   _rightAnkle;
    // Leg joints — both legs IK-owned, excluded from ragdoll toggle
    private Generic6DofJoint3D   _leftHip;
    private Generic6DofJoint3D   _leftKnee;
    private Generic6DofJoint3D   _leftAnkle;
    private Generic6DofJoint3D[] _leftLegJoints;
    private Generic6DofJoint3D[] _rightLegJoints;
    private Generic6DofJoint3D[] _torsoJoints;

    // All joints that participate in ragdoll toggle (left leg excluded)
    private Generic6DofJoint3D[] _joints;

    // Joint bodies
    private RigidBody3D _neckBody;
    private RigidBody3D _lShoulderBody;
    private RigidBody3D _rShoulderBody;
    private RigidBody3D _lElbowBody;
    private RigidBody3D _rElbowBody;
    private RigidBody3D _lWristBody;
    private RigidBody3D _rWristBody;
    private RigidBody3D _rHipBody;
    private RigidBody3D _rKneeBody;
    private RigidBody3D _rAnkleBody;
    private RigidBody3D _lHipBody;
    private RigidBody3D _lKneeBody;
    private RigidBody3D _lAnkleBody;

    // Body parts
    private RigidBody3D _head;
    private RigidBody3D _uTorso;
    private RigidBody3D _mTorso;
    private RigidBody3D _lTorso;
    private RigidBody3D _lUArm;
    private RigidBody3D _lLArm;
    private RigidBody3D _lHand;
    private RigidBody3D _rUArm;
    private RigidBody3D _rLArm;
    private RigidBody3D _rHand;
    private RigidBody3D _lULeg;
    private RigidBody3D _lLLeg;
    private RigidBody3D _lFoot;
    private RigidBody3D _rULeg;
    private RigidBody3D _rLLeg;
    private RigidBody3D _rFoot;

    // Body map
    private Dictionary<BodyGroup, List<RigidBody3D>> _bodies;

    public override void _ExitTree()
    {
        InputManager.Unsubscribe(nameof(GameAction.Jump),        onJustPressed: _onJump);
        InputManager.Unsubscribe(nameof(GameAction.Crouch),      onJustPressed: _onCrouch,      onJustReleased: _onCrouchRelease);
        InputManager.Unsubscribe(nameof(GameAction.Forward),     onJustPressed: _onForward,     onJustReleased: _onForward);
        InputManager.Unsubscribe(nameof(GameAction.Backward),    onJustPressed: _onBackward,    onJustReleased: _onBackward);
        InputManager.Unsubscribe(nameof(GameAction.StrafeLeft),  onJustPressed: _onStrafeLeft,  onJustReleased: _onStrafeLeft);
        InputManager.Unsubscribe(nameof(GameAction.StrafeRight), onJustPressed: _onStrafeRight, onJustReleased: _onStrafeRight);
        InputManager.Unsubscribe(nameof(GameAction.RotateLeft),  onJustPressed: _onRotateLeft,  onJustReleased: _onRotateLeft);
        InputManager.Unsubscribe(nameof(GameAction.RotateRight), onJustPressed: _onRotateRight, onJustReleased: _onRotateRight);
        _camera.ClearFocus();
        CameraManager.Instance.Release(_cameraClaim);
        base._ExitTree();
    }

    public override void _Ready()
    {
        // Camera — free-floating at root, tracks _uTorso via VirtualCamera.SetFocus (see GetTorsoNodes)
        _camera      = GetNode<IVirtualCamera>("Camera");
        _cameraNode  = GetNode<Node3D>("Camera");
        _cameraClaim = CameraManager.Instance.Request(_camera, priority: 20);

        _balanceController = GetNodeOrNull<BalanceController>("BalanceController");

        CallDeferred(new StringName("GetTorsoNodes"));

        // Get joints
        _neckJoint     = GetNode<Generic6DofJoint3D>("Head/Neck/NeckJoint");
        _leftShoulder  = GetNode<Generic6DofJoint3D>("LeftArm/Shoulder/ShoulderJoint");
        _leftElbow     = GetNode<Generic6DofJoint3D>("LeftArm/Elbow/ElbowJoint");
        _leftWrist     = GetNode<Generic6DofJoint3D>("LeftArm/Wrist/WristJoint");
        _rightShoulder = GetNode<Generic6DofJoint3D>("RightArm/Shoulder/ShoulderJoint");
        _rightElbow    = GetNode<Generic6DofJoint3D>("RightArm/Elbow/ElbowJoint");
        _rightWrist    = GetNode<Generic6DofJoint3D>("RightArm/Wrist/WristJoint");
        _leftHip       = GetNode<Generic6DofJoint3D>("LeftLeg/Hip/HipJoint");
        _leftKnee      = GetNode<Generic6DofJoint3D>("LeftLeg/Knee/KneeJoint");
        _leftAnkle     = GetNode<Generic6DofJoint3D>("LeftLeg/Ankle/AnkleJoint");
        _rightHip      = GetNode<Generic6DofJoint3D>("RightLeg/Hip/HipJoint");
        _rightKnee     = GetNode<Generic6DofJoint3D>("RightLeg/Knee/KneeJoint");
        _rightAnkle    = GetNode<Generic6DofJoint3D>("RightLeg/Ankle/AnkleJoint");

        // Both leg joint sets are IK-owned — excluded from the ragdoll toggle array.
        // Asymmetric stiffness (one leg IK, one passive) creates differential hip torque → yaw spin.
        _leftLegJoints  = [ _leftHip,  _leftKnee,  _leftAnkle  ];
        _rightLegJoints = [ _rightHip, _rightKnee, _rightAnkle ];

        // Everything else participates in the ragdoll toggle
        _joints =
        [
            _neckJoint,
            _leftShoulder,  _leftElbow,  _leftWrist,
            _rightShoulder, _rightElbow, _rightWrist,
        ];

        // Get joint bodies
        _neckBody      = GetNode<RigidBody3D>("Head/Neck");
        _lShoulderBody = GetNode<RigidBody3D>("LeftArm/Shoulder");
        _lElbowBody    = GetNode<RigidBody3D>("LeftArm/Elbow");
        _lWristBody    = GetNode<RigidBody3D>("LeftArm/Wrist");
        _rShoulderBody = GetNode<RigidBody3D>("RightArm/Shoulder");
        _rElbowBody    = GetNode<RigidBody3D>("RightArm/Elbow");
        _rWristBody    = GetNode<RigidBody3D>("RightArm/Wrist");
        _lHipBody      = GetNode<RigidBody3D>("LeftLeg/Hip");
        _lKneeBody     = GetNode<RigidBody3D>("LeftLeg/Knee");
        _lAnkleBody    = GetNode<RigidBody3D>("LeftLeg/Ankle");
        _rHipBody      = GetNode<RigidBody3D>("RightLeg/Hip");
        _rKneeBody     = GetNode<RigidBody3D>("RightLeg/Knee");
        _rAnkleBody    = GetNode<RigidBody3D>("RightLeg/Ankle");

        // Get body parts
        _head  = GetNode<RigidBody3D>("Head/Skull");
        _lUArm = GetNode<RigidBody3D>("LeftArm/UArm");
        _lLArm = GetNode<RigidBody3D>("LeftArm/LArm");
        _lHand = GetNode<RigidBody3D>("LeftArm/Hand");
        _rUArm = GetNode<RigidBody3D>("RightArm/UArm");
        _rLArm = GetNode<RigidBody3D>("RightArm/LArm");
        _rHand = GetNode<RigidBody3D>("RightArm/Hand");
        _lULeg = GetNode<RigidBody3D>("LeftLeg/ULeg");
        _lLLeg = GetNode<RigidBody3D>("LeftLeg/LLeg");
        _lFoot = GetNode<RigidBody3D>("LeftLeg/Foot");
        _rULeg = GetNode<RigidBody3D>("RightLeg/ULeg");
        _rLLeg = GetNode<RigidBody3D>("RightLeg/LLeg");
        _rFoot = GetNode<RigidBody3D>("RightLeg/Foot");

        // Build body map
        List<RigidBody3D> leftArm  = [_lUArm, _lLArm, _lHand];
        List<RigidBody3D> rightArm = [_rUArm, _rLArm, _rHand];
        List<RigidBody3D> leftLeg  = [_lULeg, _lLLeg, _lFoot];
        List<RigidBody3D> rightLeg = [_rULeg, _rLLeg, _rFoot];
        List<RigidBody3D> parts    = [_head, ..leftArm, ..rightArm, ..leftLeg, ..rightLeg];
        List<RigidBody3D> jointBodies =
        [
            _neckBody,
            _lShoulderBody, _lElbowBody, _lWristBody,
            _rShoulderBody, _rElbowBody, _rWristBody,
            _lHipBody,      _lKneeBody,  _lAnkleBody,
            _rHipBody,      _rKneeBody,  _rAnkleBody,
        ];
        List<RigidBody3D> all = [..parts, ..jointBodies];

        _bodies = new Dictionary<BodyGroup, List<RigidBody3D>>
        {
            [BodyGroup.LeftArm]     = leftArm,
            [BodyGroup.RightArm]    = rightArm,
            [BodyGroup.Arms]        = [..leftArm,  ..rightArm],
            [BodyGroup.LeftLeg]     = leftLeg,
            [BodyGroup.RightLeg]    = rightLeg,
            [BodyGroup.Legs]        = [..leftLeg,  ..rightLeg],
            [BodyGroup.Parts]       = parts,
            [BodyGroup.JointBodies] = jointBodies,
            [BodyGroup.All]         = all,
        };

        _restTransforms = new Dictionary<RigidBody3D, Transform3D>(all.Count);
        _restOffsets    = new Dictionary<RigidBody3D, Transform3D>(all.Count);
        foreach (var body in all)
            _restTransforms[body] = body.Transform;

        // Register input actions
        InputManager.Subscribe(nameof(GameAction.Jump),   onJustPressed: _onJump = _ => Jump());
        InputManager.Subscribe(nameof(GameAction.Crouch),
            onJustPressed:  _onCrouch        = _ => SetTPose(true),
            onJustReleased: _onCrouchRelease = _ => SetTPose(false));

        InputManager.Subscribe(nameof(GameAction.Forward),     onJustPressed: _onForward     = _ => UpdateInputDir(), onJustReleased: _onForward);
        InputManager.Subscribe(nameof(GameAction.Backward),    onJustPressed: _onBackward    = _ => UpdateInputDir(), onJustReleased: _onBackward);
        InputManager.Subscribe(nameof(GameAction.StrafeLeft),  onJustPressed: _onStrafeLeft  = _ => UpdateInputDir(), onJustReleased: _onStrafeLeft);
        InputManager.Subscribe(nameof(GameAction.StrafeRight), onJustPressed: _onStrafeRight = _ => UpdateInputDir(), onJustReleased: _onStrafeRight);
        InputManager.Subscribe(nameof(GameAction.RotateLeft),  onJustPressed: _onRotateLeft  = _ => UpdateInputDir(), onJustReleased: _onRotateLeft);
        InputManager.Subscribe(nameof(GameAction.RotateRight), onJustPressed: _onRotateRight = _ => UpdateInputDir(), onJustReleased: _onRotateRight);

        // Configure all joints with the new model:
        //   - Angular limits preserved (anatomy still applies)
        //   - Springs ALWAYS enabled (so damping is always active)
        //   - Stiffness ZERO (no T-pose pull, no equilibrium opinion)
        //   - Damping only (kills oscillation, body hangs naturally under gravity)
        //   - Left leg joints use IKStiffness so LegIK writes actually register
        //
        // Cfg(joint, stiffness, damping, xLow, xHigh, yLow, yHigh, zLow, zHigh)
        // Equal lo/hi = locked axis (damping still applied, stiffness zeroed).
        var d  = BodyDamping;
        var ed = HandFootDamping;
        var hs = HeadStiffness;
        var hd = HeadDamping;
        var ps = PassiveStiffness;
        var ik = IKStiffness;
        var ar = ArmStiffness;
        var ad = ArmDamping;

        static void Cfg(Generic6DofJoint3D j, float s, float d,
            float xL, float xH, float yL, float yH, float zL, float zH)
        {
            if (!IsInstanceValid(j)) return;

            // Linear: locked — no stretch allowed
            j.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearLimit, true);
            j.SetFlagY(Generic6DofJoint3D.Flag.EnableLinearLimit, true);
            j.SetFlagZ(Generic6DofJoint3D.Flag.EnableLinearLimit, true);
            j.SetParamX(Generic6DofJoint3D.Param.LinearLowerLimit, 0f);
            j.SetParamX(Generic6DofJoint3D.Param.LinearUpperLimit, 0f);
            j.SetParamY(Generic6DofJoint3D.Param.LinearLowerLimit, 0f);
            j.SetParamY(Generic6DofJoint3D.Param.LinearUpperLimit, 0f);
            j.SetParamZ(Generic6DofJoint3D.Param.LinearLowerLimit, 0f);
            j.SetParamZ(Generic6DofJoint3D.Param.LinearUpperLimit, 0f);

            // Angular X
            var xLocked = Mathf.IsEqualApprox(xL, xH);
            j.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularLimit, true);
            j.SetParamX(Generic6DofJoint3D.Param.AngularLowerLimit, Mathf.DegToRad(xL));
            j.SetParamX(Generic6DofJoint3D.Param.AngularUpperLimit, Mathf.DegToRad(xH));
            j.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularSpring, true);   // always on for damping
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, xLocked ? 0f : s);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringDamping,   xLocked ? 0f : d);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);

            // Angular Y
            var yLocked = Mathf.IsEqualApprox(yL, yH);
            j.SetFlagY(Generic6DofJoint3D.Flag.EnableAngularLimit, true);
            j.SetParamY(Generic6DofJoint3D.Param.AngularLowerLimit, Mathf.DegToRad(yL));
            j.SetParamY(Generic6DofJoint3D.Param.AngularUpperLimit, Mathf.DegToRad(yH));
            j.SetFlagY(Generic6DofJoint3D.Flag.EnableAngularSpring, true);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, yLocked ? 0f : s);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringDamping,   yLocked ? 0f : d);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);

            // Angular Z
            var zLocked = Mathf.IsEqualApprox(zL, zH);
            j.SetFlagZ(Generic6DofJoint3D.Flag.EnableAngularLimit, true);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularLowerLimit, Mathf.DegToRad(zL));
            j.SetParamZ(Generic6DofJoint3D.Param.AngularUpperLimit, Mathf.DegToRad(zH));
            j.SetFlagZ(Generic6DofJoint3D.Flag.EnableAngularSpring, true);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, zLocked ? 0f : s);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringDamping,   zLocked ? 0f : d);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
        }

        // Passive joints — stiffness 0, damping only. Physics + BalanceController torque
        // determines pose. These will flop naturally under gravity.
        Cfg(_neckJoint,     hs, hd, -30f,  30f,  -45f,  45f,  -30f,  30f);

        var torsoNode = GetNode<Torso>("Torso");
        foreach (var tj in torsoNode.Joints ?? [])
            Cfg(tj, SpineStiffness, d, -40f, 20f, -20f, 20f, -15f, 15f);

        Cfg(_leftShoulder,  ar, ad,  -90f,  90f,  -45f,  45f,  -90f,  90f);
        Cfg(_rightShoulder, ar, ad,  -90f,  90f,  -45f,  45f,  -90f,  90f);
        Cfg(_leftElbow,     ar, ad, -135f,   5f,    0f,   0f,    0f,   0f);
        Cfg(_rightElbow,    ar, ad, -135f,   5f,    0f,   0f,    0f,   0f);
        Cfg(_leftWrist,     ar, ed,  -30f,  30f,    0f,   0f,    0f,   0f);
        Cfg(_rightWrist,    ar, ed,  -30f,  30f,    0f,   0f,    0f,   0f);
        // Both legs IK-driven — same stiffness to keep hip forces symmetric and avoid yaw spin.
        // LegIK will overwrite AngularSpringEquilibriumPoint each physics tick.
        Cfg(_leftHip,   ik, d,   -90f,  90f,  -30f, 30f,  -45f,  45f);
        Cfg(_leftKnee,  ik, d,  -130f,   5f,    0f,  0f,    0f,   0f);
        Cfg(_leftAnkle, ik, ed,  -30f,  30f,    0f,  0f,  -15f,  15f);
        Cfg(_rightHip,  ik, d,   -90f,  90f,  -30f, 30f,  -45f,  45f);
        Cfg(_rightKnee, ik, d,  -130f,   5f,    0f,  0f,    0f,   0f);
        Cfg(_rightAnkle,ik, ed,  -30f,  30f,    0f,  0f,  -15f,  15f);

        // Body-level angular damp — applied by physics engine before constraint solving,
        // so more effective than ApplyTorque for killing unwanted spin.
        foreach (var body in _bodies[BodyGroup.Arms])
            body.AngularDamp = ArmAngularDamp;
        _head.AngularDamp = HeadAngularDamp;

        // Capture root-relative spawn offsets for reset
        var rootInv = GlobalTransform.AffineInverse();
        foreach (var body in _bodies[BodyGroup.All])
            _restOffsets[body] = rootInv * body.GlobalTransform;

        base._Ready();
    }

    private void Jump()
    {
        _lTorso.ApplyCentralImpulse(Vector3.Up * JumpForce * _lTorso.Mass);
        _balanceController?.OnJump();
    }

    // SetTPose(true)  = Ctrl held: snap all joints toward spawn/T-pose by cranking stiffness.
    //                   Equilibrium stays at 0 so the target is always the rest configuration.
    //                   BalanceController and IK are suspended — the spring does everything.
    // SetTPose(false) = Ctrl released: drop stiffness back to zero, restore active systems.
    //                   Body falls loose again under gravity; BalanceController takes back over.
    private void SetTPose(bool snapToTPose)
    {
        _isActive = !snapToTPose;

        var targetStiffness = snapToTPose ? SnapStiffness : PassiveStiffness;
        var d               = BodyDamping;

        // Apply to all ragdoll-managed joints (excludes left leg — IK owns those)
        foreach (var j in _joints)
        {
            if (!IsInstanceValid(j)) continue;
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, targetStiffness);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringDamping,   d);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, targetStiffness);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringDamping,   d);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, targetStiffness);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringDamping,   d);
        }

        // Torso (spine) joints restore to SpineStiffness on release, not PassiveStiffness.
        var spineStiffness = snapToTPose ? SnapStiffness : SpineStiffness;
        foreach (var j in _torsoJoints ?? [])
        {
            if (!IsInstanceValid(j)) continue;
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, spineStiffness);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, spineStiffness);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, spineStiffness);
        }

        // Arm joints restore to ArmStiffness (0 by default) on release — zero stiffness
        // means pure damping, no spring to oscillate and shake the hands.
        var armStiffness = snapToTPose ? SnapStiffness : ArmStiffness;
        var armDamping   = snapToTPose ? d : ArmDamping;
        foreach (var j in new[] { _leftShoulder, _leftElbow, _leftWrist,
                                   _rightShoulder, _rightElbow, _rightWrist })
        {
            if (!IsInstanceValid(j)) continue;
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, armStiffness);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringDamping,   armDamping);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, armStiffness);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringDamping,   armDamping);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, armStiffness);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringDamping,   armDamping);
        }

        // Neck joint restores to HeadStiffness on release so the head holds itself up.
        var neckStiffness = snapToTPose ? SnapStiffness : HeadStiffness;
        var neckDamping   = snapToTPose ? d : HeadDamping;
        if (IsInstanceValid(_neckJoint))
        {
            _neckJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, neckStiffness);
            _neckJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringDamping,   neckDamping);
            _neckJoint.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, neckStiffness);
            _neckJoint.SetParamY(Generic6DofJoint3D.Param.AngularSpringDamping,   neckDamping);
            _neckJoint.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, neckStiffness);
            _neckJoint.SetParamZ(Generic6DofJoint3D.Param.AngularSpringDamping,   neckDamping);
        }

        // Leg joints restore to IKStiffness (not SnapStiffness) on release
        // so LegIK can immediately take back control without a stiffness mismatch.
        var legStiffness = snapToTPose ? SnapStiffness : IKStiffness;
        foreach (var legJoints in new[] { _leftLegJoints, _rightLegJoints })
        foreach (var j in legJoints)
        {
            if (!IsInstanceValid(j)) continue;
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, legStiffness);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringDamping,   d);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, legStiffness);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringDamping,   d);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, legStiffness);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringDamping,   d);
        }

        if (snapToTPose)
            _balanceController?.Disable();
        else
            _balanceController?.Enable();

        _leftLegIK?.SetActive(!snapToTPose);
        _rightLegIK?.SetActive(!snapToTPose);
    }

    public void Reset(Transform3D spawnTransform)
    {
        // Root
        GlobalTransform = spawnTransform;

        // All physics bodies — teleport + zero velocity
        foreach (var (body, offset) in _restOffsets)
        {
            if (!IsInstanceValid(body)) continue;
            var rid = body.GetRid();
            PhysicsServer3D.BodySetState(rid, PhysicsServer3D.BodyState.Transform,       spawnTransform * offset);
            PhysicsServer3D.BodySetState(rid, PhysicsServer3D.BodyState.LinearVelocity,  Vector3.Zero);
            PhysicsServer3D.BodySetState(rid, PhysicsServer3D.BodyState.AngularVelocity, Vector3.Zero);
        }

        // Camera — snap immediately so it doesn't lerp from the old spawn position
        (_camera as VirtualCamera)?.Snap();

        // Re-enable active systems and clear stale input
        SetTPose(false);
        _balanceController?.SetInputDir(Vector3.Zero);
    }

    private void UpdateInputDir()
    {
        var vec = InputManager.GetVector(
            nameof(GameAction.StrafeLeft), nameof(GameAction.StrafeRight),
            nameof(GameAction.Forward),    nameof(GameAction.Backward));
        var dir = new Vector3(vec.X, 0f, -vec.Y);
        PluginLogger.Log(LogLevel.Debug, $"Direction was determined to be {dir}");
    }

    private void GetTorsoNodes()
    {
        var torsoNode = GetNode<Torso>("Torso");
        _uTorso = torsoNode.Top;
        _mTorso = torsoNode.Bodies[(torsoNode.SegmentCount - 1) / 2];
        _lTorso = torsoNode.Bottom;

        var torsoJoints = torsoNode.Joints ?? [];
        _torsoJoints = torsoJoints;
        _joints = [.._joints, ..torsoJoints];

        List<RigidBody3D> torso  = [..torsoNode.Bodies];
        _bodies[BodyGroup.Torso] = torso;
        _bodies[BodyGroup.All]   = [.._bodies[BodyGroup.All], ..torso];

        // Body-level angular damp on torso segments kills the spawn yaw impulse.
        // ApplyTorque fights the constraint solver; angular_damp runs before it.
        foreach (var body in torso)
            body.AngularDamp = TorsoAngularDamp;

        _balanceController?.Init(_lTorso, _uTorso);
        if (_balanceController != null)
        {
            _balanceController.PitchRollStiffness = UprightStiffness;
            _balanceController.PitchRollDamping   = UprightDamping;
            _balanceController.YawDamping         = YawDamping;
            _balanceController.ErrorSmoothing     = ErrorSmoothing;
            _balanceController.TiltDeadzone       = TiltDeadzone;
            _balanceController.StumbleAngle       = StumbleAngle;
            _balanceController.MoveForce          = MoveForce;
            _balanceController.VelocityLean       = VelocityLean;
            _balanceController.RotateTorque       = RotateTorque;
            _balanceController.RecoveryImpulse    = RecoveryImpulse;
        }
        _balanceController?.SetBodies(_bodies[BodyGroup.All]);

        // Balance bodies: torso segments only. Including head/arms causes yaw instability —
        // damping their asymmetric limb angular velocities injects torque through the joints
        // into the torso. Arms and head are stabilised by BodyDamping instead.
        _balanceController?.SetBalanceBodies(torso);

        // Construct LegIK solvers now that all nodes are valid.
        // FootTarget markers must exist in the scene at:
        //   LeftLeg/FootTarget  and  RightLeg/FootTarget
        var lFootTarget = GetNodeOrNull<Node3D>("LeftLeg/FootTarget");
        var rFootTarget = GetNodeOrNull<Node3D>("RightLeg/FootTarget");

        if (lFootTarget == null)
            GD.PushWarning("[RagdollCharacter] LeftLeg/FootTarget not found — IK solving disabled, ground detection still active");
        if (rFootTarget == null)
            GD.PushWarning("[RagdollCharacter] RightLeg/FootTarget not found — IK solving disabled, ground detection still active");

        _leftLegIK = new LegIK(
            _leftHip, _leftKnee, _leftAnkle,
            _lFoot, lFootTarget) { Label = "L" };
        _leftLegIK.Init();

        _rightLegIK = new LegIK(
            _rightHip, _rightKnee, _rightAnkle,
            _rFoot, rFootTarget) { Label = "R" };
        _rightLegIK.Init();

        _balanceController?.SetLegIK(_leftLegIK, _rightLegIK);

        // Now that _uTorso is resolved, point the camera at it.
        // VirtualCamera._Process will lerp toward _uTorso.GlobalPosition each render frame.
        _camera.SetFocus(_uTorso);
        (_camera as VirtualCamera)?.Snap();   // teleport immediately so first frame is correct

        var rootInv = GlobalTransform.AffineInverse();
        foreach (var body in torsoNode.Bodies)
            _restOffsets[body] = rootInv * body.GlobalTransform;
    }
}