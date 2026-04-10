using Godot;

namespace godototype.objects.player.ragdoll.limbs;

public class Foot
{
    public RigidBody3D Body { get; set; }
    public RigidBody3D HipBody { get; set; }
    public FootState State { get; set; }
    public Vector3 Target { get; set; }

    public float Cooldown { get; set; }

    public Foot(RigidBody3D body, RigidBody3D hipBody, FootState state, Vector3 target, float cooldown = 0f)
    {
        Body = body;
        HipBody = hipBody;
        State = state;
        Target = target;
        Cooldown = cooldown;
    }
}