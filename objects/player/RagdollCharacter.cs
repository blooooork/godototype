using Godot;
using godototype.constants;
using godototype.input;
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
        LeftArm, RightArm, Arms,
        LeftLeg, RightLeg, Legs,
    }

    [Export] public float JumpForce { get; set; } = 15f;

    private IVirtualCamera _camera;
    private CameraClaim _cameraClaim;
    // Actions
    private Action<string> _onJump;
    private Action<string> _onCrouch;
    // Raycasts
    private RayCast3D _rayForward;
    private RayCast3D _rayBack;
    private RayCast3D _rayLeft;
    private RayCast3D _rayRight;
    // Joints
    private Generic6DofJoint3D _neckJoint;
    private HingeJoint3D _leftShoulder;
    private HingeJoint3D _rightShoulder;
    private HingeJoint3D _leftElbow;
    private HingeJoint3D _rightElbow;
    private HingeJoint3D _leftWrist;
    private HingeJoint3D _rightWrist;
    private Generic6DofJoint3D _leftHip;
    private Generic6DofJoint3D _rightHip;
    private HingeJoint3D _leftKnee;
    private HingeJoint3D _rightKnee;
    private HingeJoint3D _leftAnkle;
    private HingeJoint3D _rightAnkle;
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
    private RigidBody3D _torso;
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
        _leftShoulder  = GetNode<HingeJoint3D>("LeftArm/LShoulder/LShoulderJoint");
        _leftElbow     = GetNode<HingeJoint3D>("LeftArm/LElbow/LElbowJoint");
        _leftWrist     = GetNode<HingeJoint3D>("LeftArm/LWrist/LWristJoint");
        _rightShoulder = GetNode<HingeJoint3D>("RightArm/RShoulder/RShoulderJoint");
        _rightElbow    = GetNode<HingeJoint3D>("RightArm/RElbow/RElbowJoint");
        _rightWrist    = GetNode<HingeJoint3D>("RightArm/RWrist/RWristJoint");
        _leftHip       = GetNode<Generic6DofJoint3D>("LeftLeg/LHip/LHipJoint");
        _leftKnee      = GetNode<HingeJoint3D>("LeftLeg/LKnee/LKneeJoint");
        _leftAnkle     = GetNode<HingeJoint3D>("LeftLeg/LAnkle/LAnkleJoint");
        _rightHip      = GetNode<Generic6DofJoint3D>("RightLeg/RHip/RHipJoint");
        _rightKnee     = GetNode<HingeJoint3D>("RightLeg/RKnee/RKneeJoint");
        _rightAnkle    = GetNode<HingeJoint3D>("RightLeg/RAnkle/RAnkleJoint");
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
        _head  = GetNode<RigidBody3D>("Head");
        _torso = GetNode<RigidBody3D>("Torso");
        _lUArm = GetNode<RigidBody3D>("LeftArm/LUArm");
        _lLArm = GetNode<RigidBody3D>("LeftArm/LLArm");
        _lHand = GetNode<RigidBody3D>("LeftArm/LHand");
        _rUArm = GetNode<RigidBody3D>("RightArm/RUArm");
        _rLArm = GetNode<RigidBody3D>("RightArm/RLArm");
        _rHand = GetNode<RigidBody3D>("RightArm/RHand");
        _lULeg = GetNode<RigidBody3D>("LeftLeg/LULeg");
        _lLLeg = GetNode<RigidBody3D>("LeftLeg/LLLeg");
        _lFoot = GetNode<RigidBody3D>("LeftLeg/LeftFoot");
        _rULeg = GetNode<RigidBody3D>("RightLeg/RULeg");
        _rLLeg = GetNode<RigidBody3D>("RightLeg/RLLeg");
        _rFoot = GetNode<RigidBody3D>("RightLeg/RightFoot");
        // Build body map
        List<RigidBody3D> leftArm     = [_lUArm, _lLArm, _lHand];
        List<RigidBody3D> rightArm    = [_rUArm, _rLArm, _rHand];
        List<RigidBody3D> leftLeg     = [_lULeg, _lLLeg, _lFoot];
        List<RigidBody3D> rightLeg    = [_rULeg, _rLLeg, _rFoot];
        List<RigidBody3D> parts       = [_head, _torso, ..leftArm, ..rightArm, ..leftLeg, ..rightLeg];
        List<RigidBody3D> jointBodies = [
            _neckBody,
            _lShoulderBody, _lElbowBody, _lWristBody,
            _rShoulderBody, _rElbowBody, _rWristBody,
            _lHipBody, _lKneeBody, _lAnkleBody,
            _rHipBody, _rKneeBody, _rAnkleBody,
        ];
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
            [BodyGroup.All]         = [..parts,    ..jointBodies],
        };
        _camera.SetFocus(_torso);
        // Register actions
        InputManager.Subscribe(nameof(GameAction.Jump),   onJustPressed: _onJump   = _ => Jump());
        InputManager.Subscribe(nameof(GameAction.Crouch), onJustPressed: _onCrouch = _ => Crouch());
        base._EnterTree();
    }

    public override void _ExitTree()
    {
        InputManager.Unsubscribe(nameof(GameAction.Jump),   onJustPressed: _onJump);
        InputManager.Unsubscribe(nameof(GameAction.Crouch), _onCrouch);
        _camera.ClearFocus();
        CameraManager.Instance.Release(_cameraClaim);
        base._ExitTree();
    }

    private bool IsGrounded() =>
        _rayForward.IsColliding() ||
        _rayBack.IsColliding()    ||
        _rayLeft.IsColliding()    ||
        _rayRight.IsColliding();

    private void Jump()
    {
        if (IsGrounded())
            _torso.ApplyCentralImpulse(Vector3.Up * JumpForce * _torso.Mass);
    }

    private void Crouch()
    {

    }
}
