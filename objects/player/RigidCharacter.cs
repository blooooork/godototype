using blendporter.definition;
using blendporter.service;
using Godot;
using godototype.camera;
using godototype.constants;
using godototype.input;

namespace godototype.objects.player;

public partial class RigidCharacter : RigidBody3D
{
    private RayCast3D _groundRay;
    private IVirtualCamera _virtualCamera;
    private CameraClaim _claim;

    private float _forwardInput = 0f;
    private float _strafeInput  = 0f;
    private float _rotateInput  = 0f;

    public override void _EnterTree()
    {
        _groundRay     = GetNode<RayCast3D>("GroundRay");
        _virtualCamera = GetNode<IVirtualCamera>("VirtualCamera");
        _claim = CameraManager.Instance.Request(_virtualCamera, priority: 20);

        InputManager.Subscribe(nameof(GameAction.Jump),        onJustPressed:  _ => Jump());
        InputManager.Subscribe(nameof(GameAction.Forward),     onPressed:      _ => _forwardInput =  1f, onJustReleased: _ => _forwardInput = 0f);
        InputManager.Subscribe(nameof(GameAction.Backward),    onPressed:      _ => _forwardInput = -1f, onJustReleased: _ => _forwardInput = 0f);
        InputManager.Subscribe(nameof(GameAction.StrafeLeft),  onPressed:      _ => _strafeInput  = -1f, onJustReleased: _ => _strafeInput  = 0f);
        InputManager.Subscribe(nameof(GameAction.StrafeRight), onPressed:      _ => _strafeInput  =  1f, onJustReleased: _ => _strafeInput  = 0f);
        InputManager.Subscribe(nameof(GameAction.RotateLeft),  onPressed:      _ => _rotateInput  =  1f, onJustReleased: _ => _rotateInput  = 0f);
        InputManager.Subscribe(nameof(GameAction.RotateRight), onPressed:      _ => _rotateInput  = -1f, onJustReleased: _ => _rotateInput  = 0f);

        base._EnterTree();
    }

    public override void _ExitTree()
    {
        CameraManager.Instance.Release(_claim);
        base._ExitTree();
    }

    public override void _PhysicsProcess(double delta)
    {
        ApplyStabilization();
        ApplyMovement();
        ApplyTurning();
        UpdateCameraRig();
    }

    // Pulls the ball's up vector back toward world up each frame.
    private void ApplyStabilization()
    {
        var correction = Transform.Basis.Y.Cross(Vector3.Up);
        ApplyTorque(correction * GameSettings.UprightStrength);
    }

    // Leans the ball in the move direction and drives it forward.
    private void ApplyMovement()
    {
        if (_forwardInput == 0f && _strafeInput == 0f) return;
        if (!_groundRay.IsColliding()) return;

        var forward = new Vector3(-Transform.Basis.Z.X, 0f, -Transform.Basis.Z.Z).Normalized();
        var right   = new Vector3( Transform.Basis.X.X, 0f,  Transform.Basis.X.Z).Normalized();
        var moveDir = (forward * _forwardInput + right * _strafeInput).Normalized();

        ApplyCentralForce(moveDir * GameSettings.MoveForce * Mass);
        // Vector3.Up.Cross(moveDir) gives the tilt axis so the top leans into movement.
        ApplyTorque(Vector3.Up.Cross(moveDir) * GameSettings.LeanTorque);
    }

    // Yaw only — X/Z angular velocity left to physics.
    private void ApplyTurning()
    {
        if (_rotateInput == 0f) return;
        AngularVelocity = new Vector3(AngularVelocity.X, _rotateInput * GameSettings.RotateSpeed, AngularVelocity.Z);
    }

    private void UpdateCameraRig()
    {
        var fwd = Transform.Basis.Z;
        _virtualCamera.SetRig(GlobalPosition, Mathf.Atan2(fwd.X, fwd.Z));
    }

    private void Jump()
    {
        if (_groundRay.IsColliding())
            ApplyCentralImpulse(Vector3.Up * Mass * GameSettings.JumpForce);
    }
}
