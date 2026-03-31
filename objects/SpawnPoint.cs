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
        InputManager.Subscribe(nameof (GameAction.Jump),  onJustPressed: _ => Spawn());
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
        AddChild(spawnScene.Instantiate());
        PluginLogger.Log(LogLevel.Debug, $"Spawned {spawnScene}");
    }
    
    public override void _ExitTree()
    {
        CameraManager.Instance.Release(_claim);
        base._ExitTree();
    }
}
