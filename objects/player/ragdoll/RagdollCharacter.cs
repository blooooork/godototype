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

    // Assign a RagdollSettings resource to configure this character.
    // Leave null to use all default values. Save presets as .tres files
    // (e.g. standard.tres, drunk.tres) and swap them here.
    [Export] public RagdollSettings Settings { get; set; }
    private RagdollSettings S => Settings ?? _defaults;
    private static readonly RagdollSettings _defaults = new();

    private Dictionary<RigidBody3D, Transform3D> _restTransforms;
    private Dictionary<RigidBody3D, Transform3D> _restOffsets;

    private IVirtualCamera    _camera;
    private Node3D            _cameraNode;
    private CameraClaim       _cameraClaim;
    private BalanceController _balanceController;
    private FootStepper       _footStepper;

    // Whether BalanceController + IK are actively running.
    // False = full ragdoll — physics only, no active systems.
    private bool _isActive = true;

    // Actions
    private Action<string> _onJump;
    private Action<string> _onCrouch;
    private Action<string> _onCrouchRelease;
    private Action<string> _onInteract;
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
    private Generic6DofJoint3D   _leftHip;
    private Generic6DofJoint3D   _leftKnee;
    private Generic6DofJoint3D   _leftAnkle;
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
        InputManager.Unsubscribe(nameof(GameAction.Interact),    onJustPressed: _onInteract);
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
        _footStepper       = GetNodeOrNull<FootStepper>("FootStepper");

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

        _joints =
        [
            _neckJoint,
            _leftShoulder,  _leftElbow,  _leftWrist,
            _rightShoulder, _rightElbow, _rightWrist,
            _leftHip,  _leftKnee,  _leftAnkle,
            _rightHip, _rightKnee, _rightAnkle
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
        InputManager.Subscribe(nameof(GameAction.Jump),     onJustPressed: _onJump     = _ => Jump());
        InputManager.Subscribe(nameof(GameAction.Interact), onJustPressed: _onInteract = _ => SetTPose(_isActive));
        InputManager.Subscribe(nameof(GameAction.Crouch),
            onJustPressed:  _onCrouch        = _ => Crouch(true),
            onJustReleased: _onCrouchRelease = _ => Crouch(false));

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
        //   - Leg joints: stiffness=0, damping only — hang passively
        //
        // Cfg(joint, stiffness, damping, xLow, xHigh, yLow, yHigh, zLow, zHigh)
        // Equal lo/hi = locked axis (damping still applied, stiffness zeroed).
        var d  = S.BodyDamping;
        var ed = S.HandFootDamping;
        var hs = S.HeadStiffness;
        var hd = S.HeadDamping;

        var ld = S.LegDamping;
        var ar = S.ArmStiffness;
        var ad = S.ArmDamping;

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
            Cfg(tj, S.SpineStiffness, d, -40f, 20f, -20f, 20f, -15f, 15f);

        Cfg(_leftShoulder,  ar, ad,  -90f,  90f,  -45f,  45f,  -90f,  90f);
        Cfg(_rightShoulder, ar, ad,  -90f,  90f,  -45f,  45f,  -90f,  90f);
        Cfg(_leftElbow,     ar, ad, -135f,   5f,    0f,   0f,    0f,   0f);
        Cfg(_rightElbow,    ar, ad, -135f,   5f,    0f,   0f,    0f,   0f);
        Cfg(_leftWrist,     ar, ed,  -30f,  30f,    0f,   0f,    0f,   0f);
        Cfg(_rightWrist,    ar, ed,  -30f,  30f,    0f,   0f,    0f,   0f);
        // Legs — passive spring holding stance. Stiffness keeps feet under the body so the
        // balance spring has something to work with. Without it, hip joints flop freely
        // and the character collapses regardless of torso balance.
        Cfg(_leftHip,    S.LegStiffness, ld,  -90f,  90f,  -30f, 30f,  -45f,  45f);
        Cfg(_leftKnee,   S.LegStiffness, ld,   -5f, 130f,    0f,  0f,    0f,   0f);
        Cfg(_leftAnkle,  S.LegStiffness, ed,  -30f,  30f,    0f,  0f,  -15f,  15f);
        Cfg(_rightHip,   S.LegStiffness, ld,  -90f,  90f,  -30f, 30f,  -45f,  45f);
        Cfg(_rightKnee,  S.LegStiffness, ld,   -5f, 130f,    0f,  0f,    0f,   0f);
        Cfg(_rightAnkle, S.LegStiffness, ed,  -30f,  30f,    0f,  0f,  -15f,  15f);

        // Body-level angular damp — applied by physics engine before constraint solving,
        // so more effective than ApplyTorque for killing unwanted spin.
        foreach (var body in _bodies[BodyGroup.Arms])
            body.AngularDamp = S.ArmAngularDamp;
        _head.AngularDamp = S.HeadAngularDamp;

        // Capture root-relative spawn offsets for reset
        var rootInv = GlobalTransform.AffineInverse();
        foreach (var body in _bodies[BodyGroup.All])
            _restOffsets[body] = rootInv * body.GlobalTransform;

        base._Ready();
    }

    private void Jump()
    {
        _lTorso.ApplyCentralImpulse(Vector3.Up * S.JumpForce * _lTorso.Mass);
        _balanceController?.OnJump();
    }

    private void Crouch(bool crouching)
    {
        // Knees
        _footStepper?.SetCrouching(crouching,
            Mathf.DegToRad(S.CrouchKneeAngle),
            Mathf.DegToRad(S.CrouchHipAngle),
            Mathf.DegToRad(S.CrouchAnkleAngle));

        // Stiffen spine and neck to keep the column stacked — shoulders are excluded
        // because their equilibrium is the T-pose rest position and stiffening them
        // snaps the arms out.
        var spineStiff = crouching ? S.CrouchBodyStiffness : S.SpineStiffness;
        var neckStiff  = crouching ? S.CrouchBodyStiffness : S.HeadStiffness;

        foreach (var j in _torsoJoints ?? [])
        {
            if (!IsInstanceValid(j)) continue;
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, spineStiff);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, spineStiff);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, spineStiff);
        }
        if (IsInstanceValid(_neckJoint))
        {
            _neckJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, neckStiff);
            _neckJoint.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, neckStiff);
            _neckJoint.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, neckStiff);
        }

    }

    // SetTPose(true)  = Ctrl held: snap all joints toward spawn/T-pose by cranking stiffness.
    //                   Equilibrium stays at 0 so the target is always the rest configuration.
    //                   BalanceController and IK are suspended — the spring does everything.
    // SetTPose(false) = Ctrl released: drop stiffness back to zero, restore active systems.
    //                   Body falls loose again under gravity; BalanceController takes back over.
    private void SetTPose(bool snapToTPose)
    {
        _isActive = !snapToTPose;

        var targetStiffness = snapToTPose ? S.SnapStiffness : 0f;
        var d               = S.BodyDamping;

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
        var spineStiffness = snapToTPose ? S.SnapStiffness : S.SpineStiffness;
        foreach (var j in _torsoJoints ?? [])
        {
            if (!IsInstanceValid(j)) continue;
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, spineStiffness);
            j.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, spineStiffness);
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, spineStiffness);
        }

        // Arm joints restore to ArmStiffness (0 by default) on release — zero stiffness
        // means pure damping, no spring to oscillate and shake the hands.
        var armStiffness = snapToTPose ? S.SnapStiffness : S.ArmStiffness;
        var armDamping   = snapToTPose ? d : S.ArmDamping;
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
        var neckStiffness = snapToTPose ? S.SnapStiffness : S.HeadStiffness;
        var neckDamping   = snapToTPose ? d : S.HeadDamping;
        if (IsInstanceValid(_neckJoint))
        {
            _neckJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, neckStiffness);
            _neckJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringDamping,   neckDamping);
            _neckJoint.SetParamY(Generic6DofJoint3D.Param.AngularSpringStiffness, neckStiffness);
            _neckJoint.SetParamY(Generic6DofJoint3D.Param.AngularSpringDamping,   neckDamping);
            _neckJoint.SetParamZ(Generic6DofJoint3D.Param.AngularSpringStiffness, neckStiffness);
            _neckJoint.SetParamZ(Generic6DofJoint3D.Param.AngularSpringDamping,   neckDamping);
        }

        if (snapToTPose)
            _balanceController?.Disable();
        else
            _balanceController?.Enable();
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

        // Camera — face +X (character's forward direction) then snap to position.
        // Default yaw=0 points the camera in -Z, which is 90° to the character's side.
        // -π/2 rotates the camera to face world +X so forward appears into the screen.
        (_camera as VirtualCamera)?.SetYaw(-Mathf.Pi / 2f);
        (_camera as VirtualCamera)?.Snap();

        // Re-enable active systems and clear stale input
        SetTPose(false);
        _balanceController?.SetInputDir(Vector2.Zero);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isActive || _balanceController == null || _torsoJoints == null || _lTorso == null) return;

        var leanAngle = _balanceController.LeanAngle;
        var leanDir   = _balanceController.LeanDir;

        if (_torsoJoints.Length == 0) return;
        var perJoint = S.SpineLeanFactor / _torsoJoints.Length;

        // Decompose world-space lean direction into character-local forward and right components.
        // Spine Z equilibrium = forward/backward bend, X = side bend.
        var rawRight  = _lTorso.GlobalTransform.Basis.Y;
        var rightFlat = new Vector3(rawRight.X, 0f, rawRight.Z);
        var fwdLean   = 0f;
        var sideLean  = 0f;
        if (leanAngle > 0.001f && rightFlat.LengthSquared() > 0.01f)
        {
            rightFlat = rightFlat.Normalized();
            var fwdFlat = rightFlat.Cross(Vector3.Up);
            fwdLean  = leanDir.Dot(fwdFlat)  * leanAngle * perJoint;
            sideLean = leanDir.Dot(rightFlat) * leanAngle * perJoint;
        }

        foreach (var j in _torsoJoints)
        {
            if (!IsInstanceValid(j)) continue;
            j.SetParamZ(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, fwdLean);
            j.SetParamX(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, sideLean);
        }

    }

    private void UpdateInputDir()
    {
        var vec = InputManager.GetVector(
            nameof(GameAction.StrafeLeft), nameof(GameAction.StrafeRight),
            nameof(GameAction.Forward),    nameof(GameAction.Backward));

        var rotateDir = (InputManager.IsPressed(nameof(GameAction.RotateRight)) ? 1f : 0f)
                      - (InputManager.IsPressed(nameof(GameAction.RotateLeft))  ? 1f : 0f);

        // Pass raw 2D input — BalanceController resolves the world-space direction
        // each physics tick from _anchorRestBasis so it always matches the current facing.
        _balanceController?.SetInputDir(vec, -rotateDir);
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
            body.AngularDamp = S.TorsoAngularDamp;

        // Properties must be set BEFORE Init — Init creates the balance joint
        // using PitchRollStiffness/Damping, so they need the right values first.
        if (_balanceController != null)
        {
            _balanceController.PitchRollStiffness = S.UprightStiffness;
            _balanceController.PitchRollDamping   = S.UprightDamping;
            _balanceController.YawDamping         = S.YawDamping;
            _balanceController.StumbleAngle       = S.StumbleAngle;
            _balanceController.MoveForce          = S.MoveForce;
            _balanceController.VelocityLean       = S.VelocityLean;
            _balanceController.IdleBrakingForce   = S.IdleBrakingForce;
            _balanceController.LeanRestoreForce   = S.LeanRestoreForce;
            _balanceController.LeanRestoreDamping = S.LeanRestoreDamping;
            _balanceController.TurnMaxSpeed       = S.TurnMaxSpeed;
        }
        _balanceController?.Init(_lTorso, _uTorso, _footStepper);
        _balanceController?.SetBodies(_bodies[BodyGroup.All]);

        // Balance bodies: torso segments only. Including head/arms causes yaw instability —
        // damping their asymmetric limb angular velocities injects torque through the joints
        // into the torso. Arms and head are stabilised by BodyDamping instead.
        _balanceController?.SetBalanceBodies(torso);

        if (_footStepper != null)
        {
            _footStepper.Setup(
                _lTorso,
                _lFoot,    _rFoot,
                _lULeg,    _rULeg,
                _lHipBody, _rHipBody,
                _leftHip,  _rightHip,
                _leftKnee, _rightKnee,
                _leftAnkle,_rightAnkle,
                S.LegStiffness);

            _footStepper.StepTriggerDistance = S.StepTriggerDistance;
            _footStepper.StanceFwd           = S.StanceFwd;
            _footStepper.CaptureGain         = S.CaptureGain;
            _footStepper.StepBounce          = S.StepBounce;
            _footStepper.LegLiftForce        = S.LegLiftForce;
            _footStepper.LegDriveForce       = S.LegDriveForce;
            _footStepper.LegDriveDamp        = S.LegDriveDamp;
            _footStepper.FootSpringForce     = S.FootSpringForce;
            _footStepper.FootSpringDamp      = S.FootSpringDamp;
            _footStepper.PlantTolerance         = S.PlantTolerance;
            _footStepper.StepCooldown           = S.StepCooldown;
            _footStepper.RestVelocityThreshold  = S.RestVelocityThreshold;
        }

        GetNodeOrNull<RagdollDebugOverlay>("DebugOverlay")
            ?.Setup(_bodies[BodyGroup.All], _lTorso, _uTorso, _head, _balanceController, _footStepper,
                    _lShoulderBody, _rShoulderBody, _lHipBody, _rHipBody);

        // Now that _uTorso is resolved, point the camera at it.
        // VirtualCamera._Process will lerp toward _uTorso.GlobalPosition each render frame.
        _camera.SetFocus(_uTorso);
        (_camera as VirtualCamera)?.Snap();   // teleport immediately so first frame is correct

        var rootInv = GlobalTransform.AffineInverse();
        foreach (var body in torsoNode.Bodies)
            _restOffsets[body] = rootInv * body.GlobalTransform;
    }
}