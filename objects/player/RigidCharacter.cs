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

    public override void _EnterTree()
    {
        _groundRay = GetNode<RayCast3D>("GroundRay");
        _virtualCamera = GetNode<IVirtualCamera>("VirtualCamera");
        _claim = CameraManager.Instance.Request(_virtualCamera, priority: 20);
        InputManager.Subscribe(nameof(GameAction.Jump), onJustPressed: _ => Jump());
        InputManager.Subscribe(nameof(GameAction.Forward), onPressed: _ => Forward());
        InputManager.Subscribe(nameof(GameAction.Backward), onPressed: _ => Backward());
        InputManager.Subscribe(nameof(GameAction.RotateLeft),  onPressed: _ => RotateLeft(),  onJustReleased: _ => StopRotate());
        InputManager.Subscribe(nameof(GameAction.RotateRight), onPressed: _ => RotateRight(), onJustReleased: _ => StopRotate());
        InputManager.Subscribe(nameof(GameAction.StrafeLeft), onPressed: _ => StrafeLeft());
        InputManager.Subscribe(nameof(GameAction.StrafeRight), onPressed: _ => StrafeRight());
        base._EnterTree();
    }

    public override void _ExitTree()
    {
        CameraManager.Instance.Release(_claim);
        base._ExitTree();
    }

    private void Jump()
    {
        if (_groundRay.IsColliding())
        {
            ApplyCentralImpulse(Vector3.Up * Mass * GameSettings.JumpForce);
            return;
        }
        PluginLogger.Log(LogLevel.Debug, "Bitch I'm already flying!!!!");
    }

    private void Forward()     { if (_groundRay.IsColliding()) LinearVelocity = new Vector3(-Transform.Basis.Z.X * GameSettings.MoveSpeed, LinearVelocity.Y, -Transform.Basis.Z.Z * GameSettings.MoveSpeed); }
    private void Backward()    { if (_groundRay.IsColliding()) LinearVelocity = new Vector3( Transform.Basis.Z.X * GameSettings.MoveSpeed, LinearVelocity.Y,  Transform.Basis.Z.Z * GameSettings.MoveSpeed); }
    private void StrafeLeft()  { if (_groundRay.IsColliding()) LinearVelocity = new Vector3(-Transform.Basis.X.X * GameSettings.MoveSpeed, LinearVelocity.Y, -Transform.Basis.X.Z * GameSettings.MoveSpeed); }
    private void StrafeRight() { if (_groundRay.IsColliding()) LinearVelocity = new Vector3( Transform.Basis.X.X * GameSettings.MoveSpeed, LinearVelocity.Y,  Transform.Basis.X.Z * GameSettings.MoveSpeed); }
    private void RotateLeft()  => AngularVelocity = new Vector3(0,  GameSettings.RotateSpeed, 0);
    private void RotateRight() => AngularVelocity = new Vector3(0, -GameSettings.RotateSpeed, 0);
    private void StopRotate()  => AngularVelocity = new Vector3(AngularVelocity.X, 0, AngularVelocity.Z);
}
