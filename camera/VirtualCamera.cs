using System;
using Godot;
using godototype.camera;

public partial class VirtualCamera : Node3D, IVirtualCamera
{
    private Camera3D _camera;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("CameraContainer/Camera");
    }

    public Transform3D GetDesiredTransform() => _camera.GlobalTransform;
    public float GetDesiredFov() => _camera.Fov;
    public void SetRig(Vector3 position, float yaw)
    {
        GlobalPosition = position;
        Rotation = new Vector3(0f, yaw, 0f);
    }
}
