using Godot;
using godototype.constants;
using godototype.input;
using System;
using System.Collections.Generic;
using System.Linq;
using godototype.camera;

namespace godototype.objects.player;

public partial class RagdollCharacter : Node3D
{
    private IVirtualCamera _camera;
    private CameraClaim _cameraClaim;
    // Actions
    private Action<string> _onJump;
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
    private List<Joint3D> _joints;
    // Parts
    private RigidBody3D _torso;
    
    public override void _EnterTree()
    {
        // Get camera
        _camera = GetNode<IVirtualCamera>("Camera");
        _cameraClaim = CameraManager.Instance.Request(_camera, priority: 20);
        // Register actions
        InputManager.Subscribe(nameof(GameAction.Jump), onJustPressed: _onJump = _ => Jump());
        // Get joints
        _neckJoint     = GetNode<Generic6DofJoint3D>("UpperTorso/NeckJoint");
        _leftShoulder  = GetNode<HingeJoint3D>("LeftArm/LShoulder/LShoulderJoint");
        _leftElbow     = GetNode<HingeJoint3D>("LeftArm/LElbow/LElbowJoint");
        _leftWrist     = GetNode<HingeJoint3D>("LeftArm/LWrist/LWristJoint");
        _rightShoulder = GetNode<HingeJoint3D>("RightArm/RShoulder/RShoulderJoint");
        _rightElbow    = GetNode<HingeJoint3D>("RightArm/RElbow/RElbowJoint");
        _rightWrist    = GetNode<HingeJoint3D>("RightArm/RWrist/RWristJoint");
        _leftHip       = GetNode<Generic6DofJoint3D>("LeftLeg/LeftHip/LHipJoint");
        _leftKnee      = GetNode<HingeJoint3D>("LeftLeg/LeftKnee/LKneeJoint");
        _leftAnkle     = GetNode<HingeJoint3D>("LeftLeg/LeftAnkle/LAnkleJoint");
        _rightHip      = GetNode<Generic6DofJoint3D>("RightLeg/RightHip/RHipJoint");
        _rightKnee     = GetNode<HingeJoint3D>("RightLeg/RightKnee/RKneeJoint");
        _rightAnkle    = GetNode<HingeJoint3D>("RightLeg/RightAnkle/RAnkleJoint");
        _joints = [_neckJoint, _leftShoulder, _leftElbow, _leftWrist, _rightShoulder, _rightElbow, _rightWrist, 
            _leftHip, _leftKnee, _leftAnkle, _rightHip, _rightKnee, _rightAnkle];
        // Get parts
        _torso          = GetNode<RigidBody3D>("Torso");
        _camera.SetFocus(_torso);
        base._EnterTree();
    }

    public override void _ExitTree()
    {
        InputManager.Unsubscribe(nameof(GameAction.Jump), onJustPressed: _onJump);
        _camera.ClearFocus();
        CameraManager.Instance.Release(_cameraClaim);
        base._ExitTree();
    }

    private void Jump()
    {
        _torso.ApplyCentralImpulse(Vector3.Up * 15f * _torso.Mass);
        foreach (var joint in _joints.Where(j => j.HasMethod("set_flag")))
        {
            joint.Call("set_flag", 0, true);
        }
    }
}
