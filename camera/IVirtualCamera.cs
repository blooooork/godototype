using Godot;

namespace godototype.camera;

public interface IVirtualCamera
{
    Transform3D GetDesiredTransform();
    float GetDesiredFov();
    void SetRig(Vector3 position, float yaw);
    void SetFocus(Node3D target);
    void ClearFocus();
}