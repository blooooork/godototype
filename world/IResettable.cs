using Godot;

namespace godototype.world;

public interface IResettable
{
    void Reset(Transform3D spawnTransform);
}
