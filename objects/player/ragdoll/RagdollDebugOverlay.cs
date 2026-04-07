using Godot;
using godotconsole;
using System.Collections.Generic;

namespace godototype.objects.player.ragdoll;

/// <summary>
/// Draws ragdoll debug visualisations each frame using ImmediateMesh:
///   • Yellow sphere/cross  — centre of mass
///   • Yellow dashed line   — CoM projected down to ground
///   • Yellow flat circle   — CoM footprint on ground plane
///   • Cyan line            — anchor "desired up" (what the balance spring is pulling toward)
///   • White line           — actual torso up vector
///
/// Toggle: F3  OR  console command `debug [on|off|toggle]`
/// </summary>
public partial class RagdollDebugOverlay : Node3D
{
    [Export] public bool Enabled { get => _enabled; set => SetEnabled(value); }
    private bool _enabled;
    private ImmediateMesh     _mesh;
    private MeshInstance3D    _meshInstance;
    private Color             _currentColor = Colors.White;

    private IReadOnlyList<RigidBody3D> _bodies;
    private RigidBody3D                _lTorso;
    private BalanceController          _balance;
    private FootStepper                _stepper;

    public void Setup(
        IReadOnlyList<RigidBody3D> bodies,
        RigidBody3D                lTorso,
        BalanceController          balance,
        FootStepper                stepper = null)
    {
        _bodies  = bodies;
        _lTorso  = lTorso;
        _balance = balance;
        _stepper = stepper;
    }

    public override void _Ready()
    {
        TopLevel = true;

        _mesh = new ImmediateMesh();

        var mat = new StandardMaterial3D
        {
            ShadingMode            = StandardMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest            = true,
            VertexColorUseAsAlbedo = true,
        };

        _meshInstance = new MeshInstance3D { Mesh = _mesh, MaterialOverride = mat };
        AddChild(_meshInstance);

        ConsoleManager.Register(
            "debug",
            "debug [on|off|toggle] — toggle ragdoll debug overlay (also F3)",
            args =>
            {
                var arg = args.Length > 0 ? args[0].ToLowerInvariant() : "toggle";
                SetEnabled(arg switch { "on" => true, "off" => false, _ => !_enabled });
                ConsoleManager.Print($"Debug overlay {(_enabled ? "ON" : "OFF")}");
            });
    }

    public override void _ExitTree() => ConsoleManager.Unregister("debug");

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F3 })
            SetEnabled(!_enabled);
    }

    private void SetEnabled(bool on)
    {
        _enabled = on;
        if (!_enabled) _mesh.ClearSurfaces();
    }

    public override void _Process(double delta)
    {
        _mesh.ClearSurfaces();
        if (!_enabled || _bodies == null) return;

        _mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

        var com     = ComputeCoM();
        var groundY = ComputeGroundY();

        // ── CoM sphere + ground projection ───────────────────────────────────
        Col(Colors.Yellow);
        DrawWireSphere(com, 0.05f);
        DrawCross(com, 0.12f);

        // Vertical line from CoM to ground
        var comGround = new Vector3(com.X, groundY, com.Z);
        DrawLine(com, comGround);

        // Flat circle on the ground showing the CoM footprint.
        // If the character is balanced this should be inside the foot positions;
        // when it drifts outside they need to step.
        DrawFlatCircle(comGround, 0.12f);

        // ── Anchor tilt — what the balance spring is currently pulling toward ─
        // Cyan = desired up direction from the anchor.
        // Matches world up when standing still; tilts toward input direction when moving.
        // Compare against the white torso-up line to see how far the body is from equilibrium.
        if (_balance != null)
        {
            var anchorUp = _balance.AnchorUp;
            Col(Colors.Cyan);
            DrawLine(_lTorso.GlobalPosition, _lTorso.GlobalPosition + anchorUp * 0.5f);
        }

        // ── Actual torso up ───────────────────────────────────────────────────
        if (_lTorso != null && GodotObject.IsInstanceValid(_lTorso))
        {
            Col(Colors.White);
            var up = _lTorso.GlobalTransform.Basis.X;
            DrawLine(_lTorso.GlobalPosition, _lTorso.GlobalPosition + up * 0.4f);
        }

        // ── Foot targets ─────────────────────────────────────────────────────
        // Green = left, Magenta = right.
        // Circle at target + line from foot to target when swinging.
        if (_stepper != null && _stepper.IsReady)
        {
            Col(Colors.Green);
            DrawFlatCircle(_stepper.LeftTarget,  0.08f, 12);
            DrawWireSphere(_stepper.LeftTarget,  0.04f, 8);
            if (_stepper.LeftSwing)
            {
                // Find left foot body for the swing line.
                // We just draw from target down slightly as a pin marker.
                DrawLine(_stepper.LeftTarget, _stepper.LeftTarget + Vector3.Down * 0.15f);
            }

            Col(Colors.Magenta);
            DrawFlatCircle(_stepper.RightTarget, 0.08f, 12);
            DrawWireSphere(_stepper.RightTarget, 0.04f, 8);
            if (_stepper.RightSwing)
            {
                DrawLine(_stepper.RightTarget, _stepper.RightTarget + Vector3.Down * 0.15f);
            }
        }

        _mesh.SurfaceEnd();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Vector3 ComputeCoM()
    {
        var pos   = Vector3.Zero;
        float tot = 0f;
        foreach (var b in _bodies)
        {
            if (!IsInstanceValid(b)) continue;
            pos += b.GlobalPosition * b.Mass;
            tot += b.Mass;
        }
        return tot > 0f ? pos / tot : Vector3.Zero;
    }

    /// Lowest body Y in the rig — good enough estimate of floor level.
    private float ComputeGroundY()
    {
        var minY = float.MaxValue;
        foreach (var b in _bodies)
        {
            if (!IsInstanceValid(b)) continue;
            if (b.GlobalPosition.Y < minY) minY = b.GlobalPosition.Y;
        }
        return minY < float.MaxValue ? minY : 0f;
    }

    private void Col(Color c) => _currentColor = c;

    private void DrawLine(Vector3 a, Vector3 b)
    {
        _mesh.SurfaceSetColor(_currentColor); _mesh.SurfaceAddVertex(a);
        _mesh.SurfaceSetColor(_currentColor); _mesh.SurfaceAddVertex(b);
    }

    /// Circle lying flat on the XZ plane at <paramref name="center"/>.
    private void DrawFlatCircle(Vector3 center, float radius, int seg = 16)
    {
        for (int i = 0; i < seg; i++)
        {
            float a0 = i       * Mathf.Tau / seg;
            float a1 = (i + 1) * Mathf.Tau / seg;
            DrawLine(
                center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
        }
    }

    private void DrawWireSphere(Vector3 center, float radius, int seg = 16)
    {
        for (int plane = 0; plane < 3; plane++)
        for (int i = 0; i < seg; i++)
        {
            float a0 = i       * Mathf.Tau / seg;
            float a1 = (i + 1) * Mathf.Tau / seg;
            DrawLine(CirclePoint(plane, a0) * radius + center,
                     CirclePoint(plane, a1) * radius + center);
        }
    }

    private static Vector3 CirclePoint(int plane, float angle)
    {
        float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
        return plane switch
        {
            0 => new Vector3(c, s, 0f),
            1 => new Vector3(c, 0f, s),
            _ => new Vector3(0f, c, s),
        };
    }

    private void DrawCross(Vector3 center, float size)
    {
        DrawLine(center - Vector3.Right   * size, center + Vector3.Right   * size);
        DrawLine(center - Vector3.Up      * size, center + Vector3.Up      * size);
        DrawLine(center - Vector3.Forward * size, center + Vector3.Forward * size);
    }
}
