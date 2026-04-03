using Godot;
using godototype.constants;
using godototype.input;
using godototype.objects.player.ragdoll.limbs;
using System;
using System.Collections.Generic;
using godototype.camera;

namespace godototype.objects.player;

public partial class RagdollCharacter : Node3D
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

    private IVirtualCamera _camera;
    private CameraClaim    _cameraClaim;
    // Actions
    private Action<string> _onJump;
    private Action<string> _onCrouch;
    private Action<string> _onCrouchRelease;
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
        _camera      = GetNode<IVirtualCamera>(new NodePath("Camera"));
        _cameraClaim = CameraManager.Instance.Request(_camera, priority: 20);

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
        foreach (var body in all)
            _restTransforms[body] = body.Transform;

        // Register actions
        InputManager.Subscribe(nameof(GameAction.Jump),   onJustPressed: _onJump = _ => Jump());
        InputManager.Subscribe(nameof(GameAction.Crouch),
            onJustPressed:  _onCrouch        = _ => Ragdoll(),
            onJustReleased: _onCrouchRelease = _ => StandUp());

        base._EnterTree();
    }

    public override void _ExitTree()
    {
        InputManager.Unsubscribe(nameof(GameAction.Jump),   onJustPressed: _onJump);
        InputManager.Unsubscribe(nameof(GameAction.Crouch), onJustPressed: _onCrouch, onJustReleased: _onCrouchRelease);
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

        base._Ready();
    }

    private void Jump()
    {
        _lTorso.ApplyCentralImpulse(Vector3.Up * JumpForce * _lTorso.Mass);
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
    }
}