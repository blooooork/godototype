using System;
using Godot;
using godototype.camera;

[Tool]
public partial class VirtualCamera : Node3D, IVirtualCamera
{
    private Vector3 _cameraOffset   = new Vector3(0f, 3f, 5f);
    private float   _cameraPitchDeg = -15f;


    [Export] public Vector3 CameraOffset
    {
        get => _cameraOffset;
        set { _cameraOffset = value; ApplyToContainer(); }
    }

    [Export] public float CameraPitchDeg
    {
        get => _cameraPitchDeg;
        set { _cameraPitchDeg = value; ApplyToContainer(); }
    }

    // How quickly the camera catches up to the target position (higher = faster).
    // Uses delta-scaled lerp: weight = Clamp(Smoothing * delta, 0, 1).
    // Typical range 5–15. 0 = instant snap (no smoothing).
    [Export] public float PositionSmoothing { get; set; } = 10f;

    private Camera3D _camera;
    private Node3D   _focus;
    private float    _yaw;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("CameraContainer/Camera");
        ApplyToContainer();
    }

    public override void _Process(double delta)
    {
        if (_focus == null || !GodotObject.IsInstanceValid(_focus)) return;

        // Position follows the focus. Yaw is player-driven (SetYaw) and held fixed here —
        // no auto-rotation toward the character's facing direction.
        var pos = PositionSmoothing <= 0f
            ? _focus.GlobalPosition
            : GlobalPosition.Lerp(_focus.GlobalPosition, Mathf.Clamp(PositionSmoothing * (float)delta, 0f, 1f));

        SetRig(pos, _yaw);
    }

    public void SetYaw(float yaw) => _yaw = yaw;

    /// <summary>
    /// Immediately teleport to the focus target — use after a scene reset
    /// so the camera doesn't lerp from its old position to the new spawn.
    /// </summary>
    public void Snap()
    {
        if (_focus == null || !GodotObject.IsInstanceValid(_focus)) return;
        SetRig(_focus.GlobalPosition, _yaw);
    }

    private void ApplyToContainer()
    {
        var container = GetNodeOrNull<Node3D>("CameraContainer");
        if (container == null) return;
        container.Position = _cameraOffset;
        container.Rotation = new Vector3(Mathf.DegToRad(_cameraPitchDeg), 0f, 0f);
    }

    public Transform3D GetDesiredTransform() => _camera.GlobalTransform;
    public float GetDesiredFov() => _camera.Fov;
    public void SetRig(Vector3 position, float yaw)
    {
        GlobalPosition = position;
        Rotation = new Vector3(0f, yaw, 0f);
    }
    public void SetFocus(Node3D target) => _focus = target;
    public void ClearFocus() => _focus = null;
}
