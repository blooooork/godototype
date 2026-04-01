using Godot;

using godototype.constants;
using godototype.camera;
using godototype.input;
using godototype.world;

namespace godototype.objects.player;

public partial class StandardCharacter : CharacterBody3D, IGravityBody
{
    [Export] public float JumpForce { get; set; } = 8f;

    private IVirtualCamera _virtualCamera;
    private CameraClaim _claim;

    public override void _EnterTree()
    {
        _virtualCamera = GetNode<IVirtualCamera>("VirtualCamera");
        _claim = CameraManager.Instance.Request(_virtualCamera, priority: 20);
        GravityManager.Register(this);
        InputManager.Subscribe(nameof(GameAction.Jump), onJustPressed: _ => Jump());
        base._EnterTree();
    }
    
    public override void _ExitTree()
    {
        CameraManager.Instance.Release(_claim);
        GravityManager.Unregister(this);
        base._ExitTree();
    }

    public bool IsValid() => IsInsideTree();

    public void ApplyGravity(float gravity, double delta)
    {
        if (IsOnFloor())
            return;
        var vel = Velocity;
        vel.Y -= gravity * (float)delta;
        Velocity = vel;
    }

    private void Jump()
    {
        if (IsOnFloor())
            Velocity = new Vector3(Velocity.X, JumpForce, Velocity.Z);
    }
}
