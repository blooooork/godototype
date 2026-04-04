using Godot;
using blendporter.definition;
using blendporter.service;
using godototype.constants;
using godototype.input;
using godototype.objects.player.ragdoll;
using godototype.objects.player.ragdoll.limbs;
using System;
using System.Collections.Generic;
using godototype.camera;
using godototype.world;

namespace godototype.objects.player;

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

    [Export] public float JumpForce       { get; set; } = 15f;
    [Export] public float SpringStiffness { get; set; } = 50f;
    [Export] public float SpringDamping   { get; set; } = 5f;

    private Dictionary<RigidBody3D, Transform3D> _restTransforms;
    private Dictionary<RigidBody3D, Transform3D> _restOffsets;

    private IVirtualCamera    _camera;
    private Node3D            _cameraNode;
    private Transform3D       _cameraRestTransform;
    private CameraClaim       _cameraClaim;
    private BalanceController _balanceController;
    private FootStepper       _leftFootStepper;
    private FootStepper       _rightFootStepper;
    private Action<double>    _onCameraTick;
    // Actions
    private Action<string> _onJump;
    private Action<string> _onCrouch;
    private Action<string> _onCrouchRelease;
    private Action<string> _onForward, _onBackward, _onStrafeLeft, _onStrafeRight, _onRotateLeft, _onRotateRight;
    // Joints
    private Generic6DofJoint3D   _neckJoint;
    private Generic6DofJoint3D   _leftShoulder;
    private Generic6DofJoint3D   _rightShoulder;
    private Generic6DofJoint3D   _leftElbow;
    private Generic6DofJoint3D   _rightElbow;
    private Generic6DofJoint3D   _leftWrist;
    private Generic6DofJoint3D   _rightWrist;
    private Generic6DofJoint3D   _leftHip;
    private Generic6DofJoint3D   _rightHip;
    private Generic6DofJoint3D   _leftKnee;
    private Generic6DofJoint3D   _rightKnee;
    private Generic6DofJoint3D   _leftAnkle;
    private Generic6DofJoint3D   _rightAnkle;
    private Generic6DofJoint3D[] _joints;
    // Joint bodies
    private RigidBody3D _neckBody;
    private RigidBody3D _lShoulderBody;
    private RigidBody3D _rShoulderBody;
    private RigidBody3D _lElbowBody;
    private RigidBody3D _rElbowBody;
    private RigidBody3D _lWristBody;
    private RigidBody3D _rWristBody;
    private RigidBody3D _lHipBody;
    private RigidBody3D _rHipBody;
    private RigidBody3D _lKneeBody;
    private RigidBody3D _rKneeBody;
    private RigidBody3D _lAnkleBody;
    private RigidBody3D _rAnkleBody;
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

    public override void _EnterTree()
    {
        // Get camera
        _camera              = GetNode<IVirtualCamera>(new NodePath("Torso/Segment0/Camera"));
        _cameraNode          = GetNode<Node3D>("Torso/Segment0/Camera");
        _cameraRestTransform = _cameraNode.Transform;
        _cameraClaim = CameraManager.Instance.Request(_camera, priority: 20);

        PoseManager.Register(_onCameraTick = _ =>
        {
            if (_cameraNode == null || !IsInstanceValid(_cameraNode)) return;
            var g = _cameraNode.GlobalRotation;
            _cameraNode.GlobalRotation = new Vector3(0f, g.Y, 0f);
        });

        // Optional balance/stepper components (present in v3 scene, absent in v2)
        _balanceController = GetNodeOrNull<BalanceController>("BalanceController");
        _leftFootStepper   = GetNodeOrNull<FootStepper>("LeftFootStepper");
        _rightFootStepper  = GetNodeOrNull<FootStepper>("RightFootStepper");

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

        // Torso joints come from the Torso node; spread them alongside the limb joints
        _joints =
        [
            _neckJoint,
            _leftShoulder,  _leftElbow,  _leftWrist,
            _rightShoulder, _rightElbow, _rightWrist,
            _leftHip,  _leftKnee,  _leftAnkle,
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
            [BodyGroup.All]         = all
        };

        _restTransforms = new Dictionary<RigidBody3D, Transform3D>(all.Count);
        _restOffsets    = new Dictionary<RigidBody3D, Transform3D>(all.Count);
        foreach (var body in all)
            _restTransforms[body] = body.Transform;

        // Register actions
        InputManager.Subscribe(nameof(GameAction.Jump),   onJustPressed: _onJump = _ => Jump());
        InputManager.Subscribe(nameof(GameAction.Crouch),
            onJustPressed:  _onCrouch        = _ => Ragdoll(),
            onJustReleased: _onCrouchRelease = _ => StandUp());

        InputManager.Subscribe(nameof(GameAction.Forward),     onJustPressed: _onForward     = _ => UpdateInputDir(), onJustReleased: _onForward);
        InputManager.Subscribe(nameof(GameAction.Backward),    onJustPressed: _onBackward    = _ => UpdateInputDir(), onJustReleased: _onBackward);
        InputManager.Subscribe(nameof(GameAction.StrafeLeft),  onJustPressed: _onStrafeLeft  = _ => UpdateInputDir(), onJustReleased: _onStrafeLeft);
        InputManager.Subscribe(nameof(GameAction.StrafeRight), onJustPressed: _onStrafeRight = _ => UpdateInputDir(), onJustReleased: _onStrafeRight);
        InputManager.Subscribe(nameof(GameAction.RotateLeft),  onJustPressed: _onRotateLeft  = _ => UpdateInputDir(), onJustReleased: _onRotateLeft);
        InputManager.Subscribe(nameof(GameAction.RotateRight), onJustPressed: _onRotateRight = _ => UpdateInputDir(), onJustReleased: _onRotateRight);

        base._EnterTree();
    }

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
        var s = SpringStiffness;
        var d = SpringDamping;

        // Cfg(joint, stiffness, damping, xLow, xHigh, yLow, yHigh, zLow, zHigh) — degrees
        // Equal low/high = locked axis. Spring equilibrium is always 0 (spawn = T-pose).
        static void Cfg(Generic6DofJoint3D j, float s, float d,
            float xL, float xH, float yL, float yH, float zL, float zH)
        {
            if (!IsInstanceValid(j)) return;

            j.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearLimit, true);
            j.SetFlagY(Generic6DofJoint3D.Flag.EnableLinearLimit, true);
            j.SetFlagZ(Generic6DofJoint3D.Flag.EnableLinearLimit, true);
            j.SetParamX(Generic6DofJoint3D.Param.LinearLowerLimit, 0f);
            j.SetParamX(Generic6DofJoint3D.Param.LinearUpperLimit, 0f);
            j.SetParamY(Generic6DofJoint3D.Param.LinearLowerLimit, 0f);
            j.SetParamY(Generic6DofJoint3D.Param.LinearUpperLimit, 0f);
            j.SetParamZ(Generic6DofJoint3D.Param.LinearLowerLimit, 0f);
            j.SetParamZ(Generic6DofJoint3D.Param.LinearUpperLimit, 0f);

            var xLocked = Mathf.IsEqualApprox(xL, xH);
            j.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularLimit, true);
            j.SetParamX(Generic6DofJoint3D.Param.AngularLowerLimit, Mathf.DegToRad(xL));
            j.SetParamX(Generic6DofJoint3D.Param.AngularUpperLimit, Mathf.DegToRad(xH));
            j.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularSpring, !xLocked);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, xLocked ? 0f : s);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringDamping,   xLocked ? 0f : d);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);

            var yLocked = Mathf.IsEqualApprox(yL, yH);
            j.SetFlagY(Generic6DofJoint3D.Flag.EnableAngularLimit, true);
            j.SetParamY(Generic6DofJoint3D.Param.AngularLowerLimit, Mathf.DegToRad(yL));
            j.SetParamY(Generic6DofJoint3D.Param.AngularUpperLimit, Mathf.DegToRad(yH));
            j.SetFlagY(Generic6DofJoint3D.Flag.EnableAngularSpring, !yLocked);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, yLocked ? 0f : s);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringDamping,   yLocked ? 0f : d);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);

            var zLocked = Mathf.IsEqualApprox(zL, zH);
            j.SetFlagZ(Generic6DofJoint3D.Flag.EnableAngularLimit, true);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularLowerLimit, Mathf.DegToRad(zL));
            j.SetParamZ(Generic6DofJoint3D.Param.AngularUpperLimit, Mathf.DegToRad(zH));
            j.SetFlagZ(Generic6DofJoint3D.Flag.EnableAngularSpring, !zLocked);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, zLocked ? 0f : s);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringDamping,   zLocked ? 0f : d);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
        }

        // Neck: limited ball-and-socket
        Cfg(_neckJoint,     s * 0.5f, d,  -30f,  30f,  -45f, 45f,  -30f,  30f);
        // Spine: torso joints configured uniformly — forward bend dominant, some lateral, minimal twist
        var torsoNode = GetNode<Torso>("Torso");
        foreach (var tj in torsoNode.Joints ?? [])
            Cfg(tj, s, d,  -40f, 20f,  -20f, 20f,  -15f, 15f);
        // Shoulders: ball-and-socket
        Cfg(_leftShoulder,  s, d,  -90f,  90f,  -45f, 45f,  -90f,  90f);
        Cfg(_rightShoulder, s, d,  -90f,  90f,  -45f, 45f,  -90f,  90f);
        // Elbows: hinge — bends on X only, no hyperextension
        Cfg(_leftElbow,     s, d, -135f,   5f,    0f,  0f,    0f,   0f);
        Cfg(_rightElbow,    s, d, -135f,   5f,    0f,  0f,    0f,   0f);
        // Wrists: flex/extend only
        Cfg(_leftWrist,     s, d,  -30f,  30f,    0f,  0f,    0f,   0f);
        Cfg(_rightWrist,    s, d,  -30f,  30f,    0f,  0f,    0f,   0f);
        // Hips: ball-and-socket
        Cfg(_leftHip,       s, d,  -90f,  90f,  -30f, 30f,  -45f,  45f);
        Cfg(_rightHip,      s, d,  -90f,  90f,  -30f, 30f,  -45f,  45f);
        // Knees: hinge — bends on X only, no hyperextension
        Cfg(_leftKnee,      s, d, -130f,   5f,    0f,  0f,    0f,   0f);
        Cfg(_rightKnee,     s, d, -130f,   5f,    0f,  0f,    0f,   0f);
        // Ankles: flex/extend + slight inversion
        Cfg(_leftAnkle,     s, d,  -30f,  30f,    0f,  0f,  -15f,  15f);
        Cfg(_rightAnkle,    s, d,  -30f,  30f,    0f,  0f,  -15f,  15f);

        // Capture root-relative spawn offsets for all non-torso bodies.
        // _Ready fires after SpawnPoint positions the node, so GlobalTransform is valid.
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

    private void Ragdoll()
    {
        foreach (var j in _joints)
        {
            j.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularSpring, false);
            j.SetFlagY(Generic6DofJoint3D.Flag.EnableAngularSpring, false);
            j.SetFlagZ(Generic6DofJoint3D.Flag.EnableAngularSpring, false);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, 0f);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, 0f);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, 0f);
        }
        _balanceController?.Disable();
        _leftFootStepper?.Disable();
        _rightFootStepper?.Disable();
    }

    private void StandUp()
    {
        var s = SpringStiffness;
        var d = SpringDamping;
        foreach (var j in _joints)
        {
            j.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularSpring, true);
            j.SetFlagY(Generic6DofJoint3D.Flag.EnableAngularSpring, true);
            j.SetFlagZ(Generic6DofJoint3D.Flag.EnableAngularSpring, true);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, s);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringDamping,   d);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, s);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringDamping,   d);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, s);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringDamping,   d);
        }
        _balanceController?.StandUp();
        _leftFootStepper?.Enable();
        _rightFootStepper?.Enable();
    }
    
    public void Reset(Transform3D spawnTransform)
    {
        // Root
        GlobalTransform = spawnTransform;

        // All physics bodies — teleport + zero velocity via physics server
        foreach (var (body, offset) in _restOffsets)
        {
            if (!IsInstanceValid(body)) continue;
            var rid = body.GetRid();
            PhysicsServer3D.BodySetState(rid, PhysicsServer3D.BodyState.Transform,       spawnTransform * offset);
            PhysicsServer3D.BodySetState(rid, PhysicsServer3D.BodyState.LinearVelocity,  Vector3.Zero);
            PhysicsServer3D.BodySetState(rid, PhysicsServer3D.BodyState.AngularVelocity, Vector3.Zero);
        }

        // Camera — restore local transform (clears any accumulated rotation)
        if (_cameraNode != null && IsInstanceValid(_cameraNode))
            _cameraNode.Transform = _cameraRestTransform;

        // Joint springs (restore if ragdolled), balance controller, foot steppers
        StandUp();

        // Clear stale input so the character doesn't drift after reset
        _balanceController?.SetInputDir(Vector3.Zero);
        _leftFootStepper?.SetInputDir(Vector3.Zero);
        _rightFootStepper?.SetInputDir(Vector3.Zero);
    }

    private void UpdateInputDir()
    {
        var vec    = InputManager.GetVector(
            nameof(GameAction.StrafeLeft), nameof(GameAction.StrafeRight),
            nameof(GameAction.Forward),    nameof(GameAction.Backward));
        var dir    = new Vector3(vec.X, 0f, -vec.Y); // forward = -Y in GetVector, +Z in world
        var rotate = InputManager.GetAxis(nameof(GameAction.RotateLeft), nameof(GameAction.RotateRight));

        PluginLogger.Log(LogLevel.Debug,
            $"[RC] UpdateInputDir dir={dir} rotate={rotate} " +
            $"balance={(_balanceController != null ? "ok" : "NULL")} " +
            $"lStepper={(_leftFootStepper  != null ? "ok" : "NULL")} " +
            $"rStepper={(_rightFootStepper  != null ? "ok" : "NULL")}");

        _balanceController?.SetInputDir(dir, rotate);
        _leftFootStepper?.SetInputDir(dir);
        _rightFootStepper?.SetInputDir(dir);
    }

    private void GetTorsoNodes()
    {
        var torsoNode = GetNode<Torso>("Torso");
        _uTorso = torsoNode.Top;
        _mTorso = torsoNode.Bodies[(torsoNode.SegmentCount - 1)/2];
        _lTorso = torsoNode.Bottom;
        var torsoJoints = torsoNode.Joints ?? [];
        _joints = [.._joints, ..torsoJoints];
        List<RigidBody3D> torso    = [..torsoNode.Bodies];
        _bodies[BodyGroup.Torso] = torso;
        _bodies[BodyGroup.All] = [.._bodies[BodyGroup.All], ..torso];

        _balanceController?.Init(_lTorso);
        _leftFootStepper?.Init(_lTorso, _mTorso, SpringStiffness, SpringDamping);
        _rightFootStepper?.Init(_lTorso, _mTorso, SpringStiffness, SpringDamping);

        var rootInv = GlobalTransform.AffineInverse();
        foreach (var body in torsoNode.Bodies)
            _restOffsets[body] = rootInv * body.GlobalTransform;
    }
}