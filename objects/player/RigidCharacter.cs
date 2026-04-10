using System;
using godototype.constants;
using Godot;
using godotconsole;
using godototype.camera;
using godototype.input;

namespace godototype.objects.player;

public partial class RigidCharacter : RigidBody3D
{
    private IVirtualCamera _virtualCamera;
    private CameraClaim _claim;
    private RigidBodyController _controller;

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
        _controller    = GetNode<RigidBodyController>("RigidBodyController");
        _virtualCamera = GetNode<IVirtualCamera>("VirtualCamera");
        _claim         = CameraManager.Instance.Request(_virtualCamera, priority: 20);
        _virtualCamera.SetFocus(this);

        InputManager.Subscribe(nameof(GameAction.Jump),        onJustPressed:  _onJump               = _ => _controller.Jump());
        InputManager.Subscribe(nameof(GameAction.Sprint),      onJustPressed:  _onSprintStart         = _ => _controller.IsSprinting = true,  onJustReleased: _onSprintEnd           = _ => _controller.IsSprinting = false);
        InputManager.Subscribe(nameof(GameAction.Forward),     onPressed:      _onForwardPressed      = _ => _controller.ForwardInput =  1f, onJustReleased: _onForwardReleased  = _ => _controller.ForwardInput = 0f);
        InputManager.Subscribe(nameof(GameAction.Backward),    onPressed:      _onBackwardPressed     = _ => _controller.ForwardInput = -1f, onJustReleased: _onBackwardReleased = _ => _controller.ForwardInput = 0f);
        InputManager.Subscribe(nameof(GameAction.StrafeLeft),  onPressed:      _onStrafeLeftPressed   = _ => _controller.StrafeInput = -1f, onJustReleased: _onStrafeLeftReleased  = _ => _controller.StrafeInput = 0f);
        InputManager.Subscribe(nameof(GameAction.StrafeRight), onPressed:      _onStrafeRightPressed  = _ => _controller.StrafeInput =  1f, onJustReleased: _onStrafeRightReleased = _ => _controller.StrafeInput = 0f);
        InputManager.Subscribe(nameof(GameAction.RotateLeft),  onPressed:      _onRotateLeftPressed   = _ => _controller.RotateInput =  1f, onJustReleased: _onRotateLeftReleased  = _ => _controller.RotateInput = 0f);
        InputManager.Subscribe(nameof(GameAction.RotateRight), onPressed:      _onRotateRightPressed  = _ => _controller.RotateInput = -1f, onJustReleased: _onRotateRightReleased = _ => _controller.RotateInput = 0f);

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
}
