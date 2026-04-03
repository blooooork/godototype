using Godot;

namespace godototype.objects.player.ragdoll.limbs;

[Tool]
public partial class Torso : Node3D
{
    private int   _segmentCount   = 3;
    private float _radius         = 0.15f;
    private float _segmentWidth   = 0.35f;
    private float _segmentSpacing = 0.3f;
    private float _totalMass      = 1.0f;

    // Exposed for RagdollCharacter to reference without knowing node names
    public RigidBody3D          Top         { get; private set; }
    public RigidBody3D          Bottom      { get; private set; }
    public RigidBody3D[]        Bodies      { get; private set; }
    public Generic6DofJoint3D   TopJoint    { get; private set; }
    public Generic6DofJoint3D   BottomJoint { get; private set; }
    public Generic6DofJoint3D[] Joints      { get; private set; }

    private bool _rebuildPending = false;

    [Export] public int SegmentCount
    {
        get => _segmentCount;
        set { _segmentCount = Mathf.Max(1, value); ScheduleRebuild(); }
    }

    [Export] public float Radius
    {
        get => _radius;
        set { _radius = Mathf.Max(0.01f, value); ScheduleRebuild(); }
    }

    [Export] public float SegmentWidth
    {
        get => _segmentWidth;
        set { _segmentWidth = Mathf.Max(0.01f, value); ScheduleRebuild(); }
    }

    /// <summary>Distance between segment centres. Less than 2*Radius = overlap.</summary>
    [Export] public float SegmentSpacing
    {
        get => _segmentSpacing;
        set { _segmentSpacing = Mathf.Max(0.01f, value); ScheduleRebuild(); }
    }

    /// <summary>Total mass split evenly across all segments.</summary>
    [Export] public float TotalMass
    {
        get => _totalMass;
        set { _totalMass = Mathf.Max(0.001f, value); ScheduleRebuild(); }
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            ScheduleRebuild();
            return;
        }

        // Runtime: read already-saved nodes back into the public arrays
        var bodies = new System.Collections.Generic.List<RigidBody3D>();
        var joints = new System.Collections.Generic.List<Generic6DofJoint3D>();

        foreach (var child in GetChildren())
        {
            if (child is RigidBody3D rb)        bodies.Add(rb);
            if (child is Generic6DofJoint3D j)  joints.Add(j);
        }

        Bodies = bodies.ToArray();
        Joints = joints.ToArray();

        Top         = Bodies.Length > 0 ? Bodies[0]   : null;
        Bottom      = Bodies.Length > 0 ? Bodies[^1]  : null;
        TopJoint    = Joints.Length > 0 ? Joints[0]   : null;
        BottomJoint = Joints.Length > 0 ? Joints[^1]  : null;
    }

    private void ScheduleRebuild()
    {
        if (_rebuildPending) return;
        _rebuildPending = true;
        CallDeferred(MethodName.DeferredRebuild);
    }

    private void DeferredRebuild() => DoRebuild();

    private void DoRebuild()
    {
        _rebuildPending = false;
        if (!IsInsideTree()) return;
        if (!Engine.IsEditorHint()) return;

        float effectiveWidth = Mathf.Max(_segmentWidth, _radius * 2f + 0.01f);
        float segmentMass    = _totalMass / _segmentCount;
        float startY         = (_segmentCount - 1) * _segmentSpacing / 2f;
        var   sceneRoot      = GetTree().EditedSceneRoot;

        // Collect existing children in order — preserving these avoids unique_id churn
        // which would break Anchor NodePaths on attached limbs.
        var existingBodies = new System.Collections.Generic.List<RigidBody3D>();
        var existingJoints = new System.Collections.Generic.List<Generic6DofJoint3D>();
        foreach (var child in GetChildren())
        {
            if (child is RigidBody3D rb)       existingBodies.Add(rb);
            if (child is Generic6DofJoint3D j)  existingJoints.Add(j);
        }

        // --- Segments: update in-place, append or trim from the end only ---
        Bodies = new RigidBody3D[_segmentCount];

        for (int i = 0; i < _segmentCount; i++)
        {
            RigidBody3D body;

            if (i < existingBodies.Count)
            {
                body = existingBodies[i];
            }
            else
            {
                body = new RigidBody3D { Name = $"Segment{i}" };
                AddChild(body); body.Owner = sceneRoot;
                var col  = new CollisionShape3D { Name = $"Segment{i}Col" };
                var mesh = new MeshInstance3D   { Name = $"Segment{i}Mesh" };
                body.AddChild(col);  col.Owner  = sceneRoot;
                body.AddChild(mesh); mesh.Owner = sceneRoot;
            }

            body.Mass      = segmentMass;
            body.Transform = new Transform3D(
                new Basis(new Vector3(0, 1, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1)),
                new Vector3(0, startY - i * _segmentSpacing, 0));

            foreach (var child in body.GetChildren())
            {
                if (child is CollisionShape3D col)
                    col.Shape = new CapsuleShape3D { Radius = _radius, Height = effectiveWidth };
                if (child is MeshInstance3D mesh)
                    mesh.Mesh = new CapsuleMesh { Radius = _radius, Height = effectiveWidth };
            }

            Bodies[i] = body;
        }

        for (int i = existingBodies.Count - 1; i >= _segmentCount; i--)
        {
            existingBodies[i].Owner = null;
            existingBodies[i].Free();
        }

        // --- Joints: same strategy ---
        int targetJointCount = Mathf.Max(0, _segmentCount - 1);
        Joints = new Generic6DofJoint3D[targetJointCount];

        for (int i = 0; i < targetJointCount; i++)
        {
            Generic6DofJoint3D joint;

            if (i < existingJoints.Count)
            {
                joint = existingJoints[i];
            }
            else
            {
                joint = new Generic6DofJoint3D { Name = $"Joint{i}_{i + 1}" };
                AddChild(joint); joint.Owner = sceneRoot;
            }

            joint.Position = new Vector3(0, (Bodies[i].Position.Y + Bodies[i + 1].Position.Y) / 2f, 0);
            joint.NodeA    = joint.GetPathTo(Bodies[i]);
            joint.NodeB    = joint.GetPathTo(Bodies[i + 1]);
            Joints[i]      = joint;
        }

        for (int i = existingJoints.Count - 1; i >= targetJointCount; i--)
        {
            existingJoints[i].Owner = null;
            existingJoints[i].Free();
        }

        Top         = Bodies[0];
        Bottom      = Bodies[^1];
        TopJoint    = Joints.Length > 0 ? Joints[0]  : null;
        BottomJoint = Joints.Length > 0 ? Joints[^1] : null;
    }
}