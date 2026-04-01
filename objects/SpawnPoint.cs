using blendporter.definition;
using blendporter.service;
using Godot;
using godototype.camera;
using godototype.constants;
using godototype.input;
using System;
using System.IO;

namespace godototype.objects;

public partial class SpawnPoint : Node3D
{
    private CameraClaim _claim;
    private IVirtualCamera _virtualCamera;
    private Area3D _spawnDetect;
    private CollisionShape3D _spawnDetectShape;
    private Aabb[] _eligibleAabbs;

    [Export] public PackedScene[] EligibleSpawns { get; set; }
    [Export] public bool SafeSpawn { get; set; }
    [Export] public int SpawnCount  { get; set; }
    private int _spawnCounter;

    public override void _EnterTree()
    {
        _virtualCamera = GetNode<IVirtualCamera>("VirtualCamera");
        _spawnDetect = GetNode<Area3D>("SpawnDetect");
        _spawnDetectShape = _spawnDetect.GetNode<CollisionShape3D>("CollisionShape3D");
        _claim = CameraManager.Instance.Request(_virtualCamera, priority: 10);
        InputManager.Subscribe(nameof(GameAction.Jump), onJustPressed: _ => Spawn());

        _eligibleAabbs = new Aabb[EligibleSpawns?.Length ?? 0];
        for (var i = 0; i < _eligibleAabbs.Length; i++)
        {
            var instance = EligibleSpawns[i].Instantiate<Node3D>();
            _eligibleAabbs[i] = GetCombinedAabb(instance);
            instance.Free();
        }
    }

    private void Spawn()
    {
        if (EligibleSpawns.Length == 0)
        {
            PluginLogger.Log(LogLevel.Debug, "Could not spawn; No eligible spawn types configured");
            return;
        }

        var index = (int)(GD.Randi() % (uint)EligibleSpawns.Length);
        SpawnScene(EligibleSpawns[index], _eligibleAabbs[index]);
    }

    public bool TrySpawn(string typeName)
    {
        for (var i = 0; i < (EligibleSpawns?.Length ?? 0); i++)
        {
            if (Path.GetFileNameWithoutExtension(EligibleSpawns[i].ResourcePath)
                .Equals(typeName, StringComparison.OrdinalIgnoreCase))
            {
                SpawnScene(EligibleSpawns[i], _eligibleAabbs[i]);
                return true;
            }
        }
        PluginLogger.Log(LogLevel.Warning, $"[SpawnPoint:{Name}] No eligible spawn matches '{typeName}'");
        return false;
    }

    private void SpawnScene(PackedScene scene, Aabb aabb)
    {
        if (SafeSpawn)
        {
            ((BoxShape3D)_spawnDetectShape.Shape).Size = aabb.Size;
            _spawnDetectShape.Position = aabb.GetCenter();

            var spawnOrigin = GlobalPosition + new Vector3(0, -aabb.Position.Y, 0);
            var query = new PhysicsShapeQueryParameters3D
            {
                Shape = _spawnDetectShape.Shape,
                Transform = new Transform3D(Basis.Identity, spawnOrigin + aabb.GetCenter())
            };
            if (GetWorld3D().DirectSpaceState.IntersectShape(query).Count > 0)
            {
                PluginLogger.Log(LogLevel.Debug, "Could not spawn; spawn area is occupied");
                return;
            }
        }

        var spawnedNode = scene.Instantiate<Node3D>();
        GetTree().Root.AddChild(spawnedNode);
        spawnedNode.Position = new Vector3(GlobalPosition.X, -aabb.Position.Y, GlobalPosition.Z);
        spawnedNode.Rotation = GlobalRotation;
        PluginLogger.Log(LogLevel.Debug, $"Spawned {scene}");        
        
        _spawnCounter++;
        if (SpawnCount > 0 && _spawnCounter >= SpawnCount)
            CallDeferred(MethodName.QueueFree);
    }

    /// <summary>
    /// Recursively computes the combined AABB of all <see cref="VisualInstance3D"/>
    /// descendants, in the root node's local space.
    /// </summary>
    private static Aabb GetCombinedAabb(Node root)
    {
        var combined = new Aabb();
        var first = true;

        void Accumulate(Node node)
        {
            if (node is VisualInstance3D visual)
            {
                var localAabb = visual.GetAabb();
                if (node != root && node is Node3D node3D)
                    localAabb = node3D.Transform * localAabb;

                if (first)
                {
                    combined = localAabb;
                    first = false;
                }
                else
                {
                    combined = combined.Merge(localAabb);
                }
            }

            foreach (var child in node.GetChildren())
                Accumulate(child);
        }

        Accumulate(root);
        return combined;
    }

    public override void _ExitTree()
    {
        CameraManager.Instance.Release(_claim);
        base._ExitTree();
    }
}