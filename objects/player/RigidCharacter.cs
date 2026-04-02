using System;
using godototype.constants;
using Godot;
using godotconsole;
using godototype.camera;
using godototype.input;

namespace godototype.objects.player;

public partial class RigidCharacter : RigidBody3D
{
    private RayCast3D _groundRayFront;
    private RayCast3D _groundRayLeft;
    private RayCast3D _groundRayRight;
    private RayCast3D _groundRayBack;
    private IVirtualCamera _virtualCamera;
    private CameraClaim _claim;

    [Export] public float LeanLimitDeg    { get; set; } = 20f;
    [Export] public float UprightStrength { get; set; } = 30f;
    [Export] public float IdleTiltDamping { get; set; } = 5f;
    [Export] public float MoveForce       { get; set; } = 10f;
    [Export] public float MaxSpeed        { get; set; } = 5f;
    [Export] public float SprintModifier  { get; set; } = 1.2f;
    [Export] public float LeanTorque      { get; set; } = 15f;
    [Export] public float RotateSpeed     { get; set; } = 1.5f;
    [Export] public float JumpForce       { get; set; } = 8f;

    private float _forwardInput;
    private float _strafeInput;
    private float _rotateInput;
    private bool _isSprinting;

    private Action<string> _onJump;
    private Action<string> _onSprintStart;
    private Action<string> _onSprintEnd;
    private Action<string> _onForwardPressed;
    private Action<string> _onForwardReleased;
    private Action<string> _onBackwardPressed;
    private Action<string> _onBackwardReleased;
    private Action<string> _onStrafeLeftPressed;
    private Action<string> _onStrafeLeftReleased;
    private Action<string> _onStrafeRightPressed;
    private Action<string> _onStrafeRightReleased;
    private Action<string> _onRotateLeftPressed;
    private Action<string> _onRotateLeftReleased;
    private Action<string> _onRotateRightPressed;
    private Action<string> _onRotateRightReleased;

    public override void _EnterTree()
    {
        _groundRayFront = GetNode<RayCast3D>("GroundRayFront");
        _groundRayRight = GetNode<RayCast3D>("GroundRayRight");
        _groundRayLeft  = GetNode<RayCast3D>("GroundRayLeft");
        _groundRayBack  = GetNode<RayCast3D>("GroundRayBack");
        _virtualCamera  = GetNode<IVirtualCamera>("VirtualCamera");
        _claim          = CameraManager.Instance.Request(_virtualCamera, priority: 20);
        _virtualCamera.SetFocus(this);

        InputManager.Subscribe(nameof(GameAction.Jump),        onJustPressed:  _onJump               = _ => Jump());
        InputManager.Subscribe(nameof(GameAction.Sprint),      onJustPressed:  _onSprintStart         = _ => SprintStart(),   onJustReleased: _onSprintEnd           = _ => SprintEnd());
        InputManager.Subscribe(nameof(GameAction.Forward),     onPressed:      _onForwardPressed      = _ => _forwardInput =  1f, onJustReleased: _onForwardReleased  = _ => _forwardInput = 0f);
        InputManager.Subscribe(nameof(GameAction.Backward),    onPressed:      _onBackwardPressed     = _ => _forwardInput = -1f, onJustReleased: _onBackwardReleased = _ => _forwardInput = 0f);
        InputManager.Subscribe(nameof(GameAction.StrafeLeft),  onPressed:      _onStrafeLeftPressed   = _ => _strafeInput  = -1f, onJustReleased: _onStrafeLeftReleased  = _ => _strafeInput = 0f);
        InputManager.Subscribe(nameof(GameAction.StrafeRight), onPressed:      _onStrafeRightPressed  = _ => _strafeInput  =  1f, onJustReleased: _onStrafeRightReleased = _ => _strafeInput = 0f);
        InputManager.Subscribe(nameof(GameAction.RotateLeft),  onPressed:      _onRotateLeftPressed   = _ => _rotateInput  =  1f, onJustReleased: _onRotateLeftReleased  = _ => _rotateInput = 0f);
        InputManager.Subscribe(nameof(GameAction.RotateRight), onPressed:      _onRotateRightPressed  = _ => _rotateInput  = -1f, onJustReleased: _onRotateRightReleased = _ => _rotateInput = 0f);

        StatsManager.Register(this, "speed", () => $"{LinearVelocity.Length():F1} m/s");
        base._EnterTree();
    }

    public override void _ExitTree()
    {
        InputManager.Unsubscribe(nameof(GameAction.Jump),        onJustPressed:  _onJump);
        InputManager.Unsubscribe(nameof(GameAction.Sprint),      onJustPressed:  _onSprintStart,        onJustReleased: _onSprintEnd);
        InputManager.Unsubscribe(nameof(GameAction.Forward),     onPressed:      _onForwardPressed,     onJustReleased: _onForwardReleased);
        InputManager.Unsubscribe(nameof(GameAction.Backward),    onPressed:      _onBackwardPressed,    onJustReleased: _onBackwardReleased);
        InputManager.Unsubscribe(nameof(GameAction.StrafeLeft),  onPressed:      _onStrafeLeftPressed,  onJustReleased: _onStrafeLeftReleased);
        InputManager.Unsubscribe(nameof(GameAction.StrafeRight), onPressed:      _onStrafeRightPressed, onJustReleased: _onStrafeRightReleased);
        InputManager.Unsubscribe(nameof(GameAction.RotateLeft),  onPressed:      _onRotateLeftPressed,  onJustReleased: _onRotateLeftReleased);
        InputManager.Unsubscribe(nameof(GameAction.RotateRight), onPressed:      _onRotateRightPressed, onJustReleased: _onRotateRightReleased);
        _virtualCamera.ClearFocus();
        CameraManager.Instance.Release(_claim);
        StatsManager.Unregister(this);
        base._ExitTree();
    }

    public override void _PhysicsProcess(double delta)
    {
        ApplyStabilization();
        ApplyMovement();
        ApplyTurning();
    }

    // Pulls the ball's up vector back toward world up each frame.
    private void ApplyStabilization()
    {
        var correction = Transform.Basis.Y.Cross(Vector3.Up);
        ApplyTorque(correction * UprightStrength);

        bool hasInput = _forwardInput != 0f || _strafeInput != 0f;
        if (!hasInput)
        {
            // Damp tilt angular velocity on X/Z to kill wobble when idle.
            var tiltDamp = new Vector3(-AngularVelocity.X, 0f, -AngularVelocity.Z) * IdleTiltDamping;
            ApplyTorque(tiltDamp);
        }
    }

    private bool IsGrounded()
    {
        return _groundRayFront.IsColliding() ||  _groundRayRight.IsColliding() ||  _groundRayLeft.IsColliding() || _groundRayBack.IsColliding();
    }
    
    // Leans the ball in the move direction and drives it forward.
    private void ApplyMovement()
    {
        if (_forwardInput == 0f && _strafeInput == 0f) return;
        if (!IsGrounded()) return;

        var forward = new Vector3(-Transform.Basis.Z.X, 0f, -Transform.Basis.Z.Z).Normalized();
        var right   = new Vector3( Transform.Basis.X.X, 0f,  Transform.Basis.X.Z).Normalized();
        var moveDir = (forward * _forwardInput + right * _strafeInput).Normalized();

        var speedCap = MaxSpeed * (_isSprinting ? SprintModifier : 1f);
        var horizontalSpeed = new Vector3(LinearVelocity.X, 0f, LinearVelocity.Z).Length();
        if (horizontalSpeed >= speedCap) return;

        var finalMoveForce = MoveForce * (_isSprinting ? SprintModifier : 1f);
        ApplyCentralForce(moveDir * finalMoveForce * Mass);

        var tiltAngle = Mathf.Acos(Mathf.Clamp(Transform.Basis.Y.Dot(Vector3.Up), -1f, 1f));
        if (tiltAngle < Mathf.DegToRad(LeanLimitDeg))
            ApplyTorque(Vector3.Up.Cross(moveDir) * LeanTorque);
    }

    // Yaw only — X/Z angular velocity left to physics.
    private void ApplyTurning()
    {
        if (_rotateInput == 0f) return;
        AngularVelocity = new Vector3(AngularVelocity.X, _rotateInput * RotateSpeed, AngularVelocity.Z);
    }

    private void Jump()
    {
        if (IsGrounded())
            ApplyCentralImpulse(Vector3.Up * Mass * JumpForce);
    }

    private void SprintStart()
    {
        _isSprinting = true;
    }
    
    private void SprintEnd()
    {
        _isSprinting = false;
    }
}
