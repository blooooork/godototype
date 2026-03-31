using blendporter.definition;
using blendporter.service;
using Godot;
using godototype.camera;
using godototype.constants;
using godototype.input;

namespace godototype.objects;

public partial class SpawnPoint : Node3D
{
    private CameraClaim _claim;
    private IVirtualCamera _virtualCamera;
    [Export] public PackedScene[] EligibleSpawns { get; set; }

    public override void _EnterTree()
    {
        _virtualCamera = GetNode<IVirtualCamera>("VirtualCamera");
        _claim = CameraManager.Instance.Request(_virtualCamera, priority: 10);
        InputManager.Subscribe(nameof(GameAction.Jump), onJustPressed: _ => Spawn());
    }

    private void Spawn()
    {
        if (EligibleSpawns.Length == 0)
        {
            PluginLogger.Log(LogLevel.Debug, "Could not spawn; No eligible spawn types configured");
            return;
        }
        // TODO make this random once we have more; Or make a menu pop up
        var spawnScene = EligibleSpawns[0];
        var spawnedNode = spawnScene.Instantiate<Node3D>();
        var aabb = GetCombinedAabb(spawnedNode);
        var height = aabb.Size.Y;
        spawnedNode.Position = new Vector3(0, -aabb.Position.Y, 0);
        AddChild(spawnedNode);
        PluginLogger.Log(LogLevel.Debug, $"Spawned {spawnScene}");
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