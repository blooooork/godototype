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

    private Camera3D _camera;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("CameraContainer/Camera");
        ApplyToContainer();
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
}
