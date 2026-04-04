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

    [Export] public float JumpForce      { get; set; } = 15f;

    // PassiveStiffness: tiny baseline stiffness on all non-IK joints.
    // Keeps the body from flopping completely before BalanceController gets traction.
    // 0 = fully loose. 1-3 = light resistance. Don't go high or it fights BalanceController.
    [Export] public float PassiveStiffness { get; set; } = 0f;

    // IkStiffness: how strongly IK equilibrium writes are tracked.
    // This is NOT a restore-to-T-pose spring — it's the gain on LegIK/BalanceController
    // equilibrium targets. Keep this low; physics and balance torque do the heavy lifting.
    [Export] public float IkStiffness    { get; set; } = 8f;

    // JointDamping: kills oscillation on all joints without pulling toward any pose.
    // Too low = joints wobble forever. Too high = sluggish, puppet-like.
    // Kept intentionally low so damping doesn't cancel out spring corrective force.
    [Export] public float JointDamping   { get; set; } = 1f;

    // TPoseStiffness: stiffness applied to all joints when Ctrl is held.
    // High enough to visibly snap to T-pose against gravity; equilibrium stays at 0
    // (spawn pose) so this is always a snap to the rest configuration.
    [Export] public float TPoseStiffness { get; set; } = 120f;

    private Dictionary<RigidBody3D, Transform3D> _restTransforms;
    private Dictionary<RigidBody3D, Transform3D> _restOffsets;

    private IVirtualCamera    _camera;
    private Node3D            _cameraNode;
    private Transform3D       _cameraRestTransform;
    private CameraClaim       _cameraClaim;
    private BalanceController _balanceController;
    private LegIK             _leftLegIK;
    private LegIK             _rightLegIK;
    private Action<double>    _onCameraTick;

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
    // Left leg joints — owned by LegIK, excluded from ragdoll toggle
    private Generic6DofJoint3D   _leftHip;
    private Generic6DofJoint3D   _leftKnee;
    private Generic6DofJoint3D   _leftAnkle;
    private Generic6DofJoint3D[] _leftLegJoints;

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
        PoseManager.Unregister(_onCameraTick);
        _camera.ClearFocus();
        CameraManager.Instance.Release(_cameraClaim);
        base._ExitTree();
    }

    public override void _Ready()
    {
        // Camera
        _camera              = GetNode<IVirtualCamera>(new NodePath("Torso/Segment0/Camera"));
        _cameraNode          = GetNode<Node3D>("Torso/Segment0/Camera");
        _cameraRestTransform = _cameraNode.Transform;
        _cameraClaim         = CameraManager.Instance.Request(_camera, priority: 20);

        PoseManager.Register(_onCameraTick = _ =>
        {
            if (_cameraNode == null || !IsInstanceValid(_cameraNode)) return;
            var g = _cameraNode.GlobalRotation;
            _cameraNode.GlobalRotation = new Vector3(0f, g.Y, 0f);
        });

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

        // Left leg joints are IK-owned — excluded from the ragdoll toggle array
        _leftLegJoints = [ _leftHip, _leftKnee, _leftAnkle ];

        // Everything else participates in the ragdoll toggle
        _joints =
        [
            _neckJoint,
            _leftShoulder,  _leftElbow,  _leftWrist,
            _rightShoulder, _rightElbow, _rightWrist,
            _rightHip, _rightKnee, _rightAnkle,
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
        //   - Left leg joints use IkStiffness so LegIK writes actually register
        //
        // Cfg(joint, stiffness, damping, xLow, xHigh, yLow, yHigh, zLow, zHigh)
        // Equal lo/hi = locked axis (damping still applied, stiffness zeroed).
        var d  = JointDamping;
        var ps = PassiveStiffness;
        var ik = IkStiffness;

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
        Cfg(_neckJoint,     ps, d,  -30f,  30f,  -45f,  45f,  -30f,  30f);

        var torsoNode = GetNode<Torso>("Torso");
        foreach (var tj in torsoNode.Joints ?? [])
            Cfg(tj,         ps, d,  -40f,  20f,  -20f,  20f,  -15f,  15f);

        Cfg(_leftShoulder,  ps, d,  -90f,  90f,  -45f,  45f,  -90f,  90f);
        Cfg(_rightShoulder, ps, d,  -90f,  90f,  -45f,  45f,  -90f,  90f);
        Cfg(_leftElbow,     ps, d, -135f,   5f,    0f,   0f,    0f,   0f);
        Cfg(_rightElbow,    ps, d, -135f,   5f,    0f,   0f,    0f,   0f);
        Cfg(_leftWrist,     ps, d,  -30f,  30f,    0f,   0f,    0f,   0f);
        Cfg(_rightWrist,    ps, d,  -30f,  30f,    0f,   0f,    0f,   0f);
        Cfg(_rightHip,      ps, d,  -90f,  90f,  -30f,  30f,  -45f,  45f);
        Cfg(_rightKnee,     ps, d, -130f,   5f,    0f,   0f,    0f,   0f);
        Cfg(_rightAnkle,    ps, d,  -30f,  30f,    0f,   0f,  -15f,  15f);

        // IK-driven joints — low stiffness so LegIK equilibrium writes land,
        // but still loose enough that physics can override on hard impacts.
        // LegIK will overwrite AngularSpringEquilibriumPoint each physics tick.
        Cfg(_leftHip,   ik, d,  -90f,  90f,  -30f, 30f,  -45f,  45f);
        Cfg(_leftKnee,  ik, d, -130f,   5f,    0f,  0f,    0f,   0f);
        Cfg(_leftAnkle, ik, d,  -30f,  30f,    0f,  0f,  -15f,  15f);

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

        var targetStiffness = snapToTPose ? TPoseStiffness : PassiveStiffness;
        var d               = JointDamping;

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

        // Left leg joints restore to IkStiffness (not TPoseStiffness) on release
        // so LegIK can immediately take back control without a stiffness mismatch.
        var leftLegStiffness = snapToTPose ? TPoseStiffness : IkStiffness;
        foreach (var j in _leftLegJoints)
        {
            if (!IsInstanceValid(j)) continue;
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, leftLegStiffness);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringDamping,   d);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, leftLegStiffness);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringDamping,   d);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, leftLegStiffness);
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

        // Camera — restore local transform
        if (_cameraNode != null && IsInstanceValid(_cameraNode))
            _cameraNode.Transform = _cameraRestTransform;

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
        _joints = [.._joints, ..torsoJoints];

        List<RigidBody3D> torso  = [..torsoNode.Bodies];
        _bodies[BodyGroup.Torso] = torso;
        _bodies[BodyGroup.All]   = [.._bodies[BodyGroup.All], ..torso];

        _balanceController?.Init(_lTorso, _uTorso);
        _balanceController?.SetBodies(_bodies[BodyGroup.All]);

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

        var rootInv = GlobalTransform.AffineInverse();
        foreach (var body in torsoNode.Bodies)
            _restOffsets[body] = rootInv * body.GlobalTransform;
    }
}