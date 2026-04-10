using blendporter.definition;
using blendporter.service;
using Godot;
using godotconsole;
using System.Collections.Generic;

namespace godototype.camera;

public partial class CameraManager : Node
{
    public static CameraManager Instance { get; private set; }
    
    private Camera3D _actualCamera;
    private readonly List<CameraClaim> _suitors = [];
    private Transform3D _blendFromXform;
    private float _blendFromFov = 75f;
    private float _blendTimer;
    private float _blendDuration;
    private bool _isBlending;

    public override void _EnterTree()
    {
        Instance = this;
        base._EnterTree();
    }

    public override void _Ready()
    {
        _actualCamera = new Camera3D { Name = new StringName("PrimaryCamera") };
        AddChild(_actualCamera);
        _actualCamera.MakeCurrent();
    }

    public override void _Process(double delta)
    {
        if(_suitors.Count == 0) return;
        var target = _suitors[^1].Source;
        var targetXform = target.GetDesiredTransform();
        var targetFov = target.GetDesiredFov();

        if (_isBlending)
        {
            _blendTimer += (float)delta;
            var t = Mathf.Clamp(_blendTimer / _blendDuration, 0f, 1f);
            t = t * t * (3f - 2f * t); // smoothstep

            _actualCamera.GlobalTransform = _blendFromXform.InterpolateWith(targetXform, t);
            _actualCamera.Fov = Mathf.Lerp(_blendFromFov, targetFov, t);

            if (t >= 1f) _isBlending = false;
        }
        else
        {
            _actualCamera.GlobalTransform = targetXform;
            _actualCamera.Fov = targetFov;
        }
    }
    
    public CameraClaim Request(IVirtualCamera source, int priority = 10, float blendIn = 0.4f)
    {
        var claim = new CameraClaim(source, priority, blendIn);
        var oldTop = _suitors.Count > 0 ? _suitors[^1].Source : null;

        // Insert sorted by priority (lowest first, highest on top)
        var idx = 0;
        for (var i = 0; i < _suitors.Count; i++)
        {
            if (_suitors[i].Priority <= priority) idx = i + 1;
        }
        _suitors.Insert(idx, claim);

        if (_suitors[^1].Source != oldTop)
        {
            PluginLogger.Log(LogLevel.Debug, $"Camera switched to {CameraName(_suitors[^1].Source)} (priority {priority})");
            UpdateFocusedEntity();
            StartBlend(blendIn);
        }

        return claim;
    }

    public void Release(CameraClaim claim, float blendOut = 0.4f)
    {
        var oldTop = _suitors.Count > 0 ? _suitors[^1].Source : null;
        _suitors.Remove(claim);

        if (_suitors.Count == 0) return;

        if (_suitors[^1].Source != oldTop)
        {
            var newTop = _suitors[^1];
            PluginLogger.Log(LogLevel.Debug, $"Camera switched to {CameraName(newTop.Source)} (priority {newTop.Priority}) after release");
            UpdateFocusedEntity();
            StartBlend(blendOut);
        }
    }

    private void UpdateFocusedEntity()
    {
        var top = _suitors.Count > 0 ? _suitors[^1].Source : null;
        StatsManager.FocusedEntity = top is Node n ? n.GetParent() : null;
    }

    private static string CameraName(IVirtualCamera cam) =>
        cam is Node node ? node.Name.ToString() : cam.GetType().Name;

    private void StartBlend(float duration)
    {
        if(_actualCamera == null) return;
        _blendFromXform = _actualCamera.GlobalTransform;
        _blendFromFov = _actualCamera.Fov;
        _blendDuration = Mathf.Max(duration, 0.01f);
        _blendTimer = 0f;
        _isBlending = true;
    }
}
