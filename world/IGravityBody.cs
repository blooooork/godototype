namespace godototype.world;

public interface IGravityBody
{
    /// <summary>Return false if the node has been freed/removed.</summary>
    bool IsValid();

    /// <summary>Apply gravity however this body needs it.</summary>
    void ApplyGravity(float gravity, double delta);
}