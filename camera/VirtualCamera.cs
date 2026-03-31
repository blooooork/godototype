using System;
using Godot;
using godototype.camera;

public partial class VirtualCamera : Camera3D, IVirtualCamera
{
    
    public Transform3D GetDesiredTransform()
    {
        return GlobalTransform;
    }

    public float GetDesiredFov()
    {
        return GetFov();
    }
}
