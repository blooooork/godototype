using Godot;
using godototype.constants;
using godototype.input;
using godototype.world;
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

    [Export] public float JumpForce      { get; set; } = 15f;
    [Export] public float SpringStiffness { get; set; } = 50f;
    [Export] public float SpringDamping   { get; set; } = 5f;

    private Dictionary<RigidBody3D, Transform3D> _restTransforms;

    private IVirtualCamera _camera;
    private CameraClaim _cameraClaim;
    // Actions
    private Action<string> _onJump;
    private Action<string> _onCrouch;
    private Action<string> _onCrouchRelease;
    // Raycasts
    private RayCast3D _rayForward;
    private RayCast3D _rayBack;
    private RayCast3D _rayLeft;
    private RayCast3D _rayRight;
    // Joints
    private Generic6DofJoint3D _neckJoint;
    private Generic6DofJoint3D _uTorsoJoint;
    private Generic6DofJoint3D _lTorsoJoint;
    private Generic6DofJoint3D _leftShoulder;
    private Generic6DofJoint3D _rightShoulder;
    private Generic6DofJoint3D _leftElbow;
    private Generic6DofJoint3D _rightElbow;
    private Generic6DofJoint3D _leftWrist;
    private Generic6DofJoint3D _rightWrist;
    private Generic6DofJoint3D _leftHip;
    private Generic6DofJoint3D _rightHip;
    private Generic6DofJoint3D _leftKnee;
    private Generic6DofJoint3D _rightKnee;
    private Generic6DofJoint3D _leftAnkle;
    private Generic6DofJoint3D _rightAnkle;
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
        _camera = GetNode<IVirtualCamera>("Camera");
        _cameraClaim = CameraManager.Instance.Request(_camera, priority: 20);
        // Get raycasts
        _rayForward = GetNode<RayCast3D>("ForwardRay");
        _rayBack    = GetNode<RayCast3D>("BackRay");
        _rayLeft    = GetNode<RayCast3D>("LeftRay");
        _rayRight   = GetNode<RayCast3D>("RightRay");
        // Get joints
        _neckJoint     = GetNode<Generic6DofJoint3D>("Neck/NeckJoint");
        _uTorsoJoint   = GetNode<Generic6DofJoint3D>("UTorsoJoint");
        _lTorsoJoint   = GetNode<Generic6DofJoint3D>("LTorsoJoint");
        _leftShoulder  = GetNode<Generic6DofJoint3D>("LeftArm/LShoulder/LShoulderJoint");
        _leftElbow     = GetNode<Generic6DofJoint3D>("LeftArm/LElbow/LElbowJoint");
        _leftWrist     = GetNode<Generic6DofJoint3D>("LeftArm/LWrist/LWristJoint");
        _rightShoulder = GetNode<Generic6DofJoint3D>("RightArm/RShoulder/RShoulderJoint");
        _rightElbow    = GetNode<Generic6DofJoint3D>("RightArm/RElbow/RElbowJoint");
        _rightWrist    = GetNode<Generic6DofJoint3D>("RightArm/RWrist/RWristJoint");
        _leftHip       = GetNode<Generic6DofJoint3D>("LeftLeg/LHip/LHipJoint");
        _leftKnee      = GetNode<Generic6DofJoint3D>("LeftLeg/LKnee/LKneeJoint");
        _leftAnkle     = GetNode<Generic6DofJoint3D>("LeftLeg/LAnkle/LAnkleJoint");
        _rightHip      = GetNode<Generic6DofJoint3D>("RightLeg/RHip/RHipJoint");
        _rightKnee     = GetNode<Generic6DofJoint3D>("RightLeg/RKnee/RKneeJoint");
        _rightAnkle    = GetNode<Generic6DofJoint3D>("RightLeg/RAnkle/RAnkleJoint");
        _joints =
        [
            _neckJoint,
            _uTorsoJoint, _lTorsoJoint,
            _leftShoulder, _leftElbow, _leftWrist,
            _rightShoulder, _rightElbow, _rightWrist,
            _leftHip, _leftKnee, _leftAnkle,
            _rightHip, _rightKnee, _rightAnkle,
        ];
        // Get joint bodies
        _neckBody      = GetNode<RigidBody3D>("Neck");
        _lShoulderBody = GetNode<RigidBody3D>("LeftArm/LShoulder");
        _lElbowBody    = GetNode<RigidBody3D>("LeftArm/LElbow");
        _lWristBody    = GetNode<RigidBody3D>("LeftArm/LWrist");
        _rShoulderBody = GetNode<RigidBody3D>("RightArm/RShoulder");
        _rElbowBody    = GetNode<RigidBody3D>("RightArm/RElbow");
        _rWristBody    = GetNode<RigidBody3D>("RightArm/RWrist");
        _lHipBody      = GetNode<RigidBody3D>("LeftLeg/LHip");
        _lKneeBody     = GetNode<RigidBody3D>("LeftLeg/LKnee");
        _lAnkleBody    = GetNode<RigidBody3D>("LeftLeg/LAnkle");
        _rHipBody      = GetNode<RigidBody3D>("RightLeg/RHip");
        _rKneeBody     = GetNode<RigidBody3D>("RightLeg/RKnee");
        _rAnkleBody    = GetNode<RigidBody3D>("RightLeg/RAnkle");
        // Get body parts
        _head   = GetNode<RigidBody3D>("Head");
        _uTorso = GetNode<RigidBody3D>("UTorso");
        _mTorso = GetNode<RigidBody3D>("MTorso");
        _lTorso = GetNode<RigidBody3D>("LTorso");
        _lUArm = GetNode<RigidBody3D>("LeftArm/LUArm");
        _lLArm = GetNode<RigidBody3D>("LeftArm/LLArm");
        _lHand = GetNode<RigidBody3D>("LeftArm/LHand");
        _rUArm = GetNode<RigidBody3D>("RightArm/RUArm");
        _rLArm = GetNode<RigidBody3D>("RightArm/RLArm");
        _rHand = GetNode<RigidBody3D>("RightArm/RHand");
        _lULeg = GetNode<RigidBody3D>("LeftLeg/LULeg");
        _lLLeg = GetNode<RigidBody3D>("LeftLeg/LLLeg");
        _lFoot = GetNode<RigidBody3D>("LeftLeg/LFoot");
        _rULeg = GetNode<RigidBody3D>("RightLeg/RULeg");
        _rLLeg = GetNode<RigidBody3D>("RightLeg/RLLeg");
        _rFoot = GetNode<RigidBody3D>("RightLeg/RFoot");
        // Build body map
        List<RigidBody3D> torso       = [_uTorso, _mTorso, _lTorso];
        List<RigidBody3D> leftArm     = [_lUArm, _lLArm, _lHand];
        List<RigidBody3D> rightArm    = [_rUArm, _rLArm, _rHand];
        List<RigidBody3D> leftLeg     = [_lULeg, _lLLeg, _lFoot];
        List<RigidBody3D> rightLeg    = [_rULeg, _rLLeg, _rFoot];
        List<RigidBody3D> parts       = [_head, ..torso, ..leftArm, ..rightArm, ..leftLeg, ..rightLeg];
        List<RigidBody3D> jointBodies = [
            _neckBody,
            _lShoulderBody, _lElbowBody, _lWristBody,
            _rShoulderBody, _rElbowBody, _rWristBody,
            _lHipBody, _lKneeBody, _lAnkleBody,
            _rHipBody, _rKneeBody, _rAnkleBody,
        ];
        List<RigidBody3D> all = [..parts, ..jointBodies];
        _bodies = new Dictionary<BodyGroup, List<RigidBody3D>>
        {
            [BodyGroup.Torso]       = torso,
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
        Callable.From(() => _camera.SetFocus(_uTorso)).CallDeferred();
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
            // Lock all linear axes
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
        Cfg(_neckJoint,     s * 0.5f, d,  -30f, 30f,  -45f, 45f,  -30f, 30f);
        // Spine: forward bend dominant, some lateral, minimal twist
        Cfg(_uTorsoJoint,   s, d,  -40f, 15f,  -20f, 20f,  -20f, 20f);
        Cfg(_lTorsoJoint,   s, d,  -45f, 20f,  -15f, 15f,  -10f, 10f);
        // Shoulders: ball-and-socket
        Cfg(_leftShoulder,  s, d,  -90f, 90f,  -45f, 45f,  -90f, 90f);
        Cfg(_rightShoulder, s, d,  -90f, 90f,  -45f, 45f,  -90f, 90f);
        // Elbows: hinge — bends on X only, no hyperextension
        Cfg(_leftElbow,     s, d, -135f,  5f,    0f,  0f,    0f,  0f);
        Cfg(_rightElbow,    s, d, -135f,  5f,    0f,  0f,    0f,  0f);
        // Wrists: flex/extend only
        Cfg(_leftWrist,     s, d,  -30f, 30f,    0f,  0f,    0f,  0f);
        Cfg(_rightWrist,    s, d,  -30f, 30f,    0f,  0f,    0f,  0f);
        // Hips: ball-and-socket
        Cfg(_leftHip,       s, d,  -90f, 90f,  -30f, 30f,  -45f, 45f);
        Cfg(_rightHip,      s, d,  -90f, 90f,  -30f, 30f,  -45f, 45f);
        // Knees: hinge — bends on X only, no hyperextension
        Cfg(_leftKnee,      s, d, -130f,  5f,    0f,  0f,    0f,  0f);
        Cfg(_rightKnee,     s, d, -130f,  5f,    0f,  0f,    0f,  0f);
        // Ankles: flex/extend + slight inversion
        Cfg(_leftAnkle,     s, d,  -30f, 30f,    0f,  0f,  -15f, 15f);
        Cfg(_rightAnkle,    s, d,  -30f, 30f,    0f,  0f,  -15f, 15f);

        base._Ready();
    }

    private bool IsGrounded() =>
        _rayForward.IsColliding() ||
        _rayBack.IsColliding()    ||
        _rayLeft.IsColliding()    ||
        _rayRight.IsColliding();

    private void Jump()
    {
        if (IsGrounded())
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
}
