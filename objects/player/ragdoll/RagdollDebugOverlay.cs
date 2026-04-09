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
///   • Cornflower blue bar  — hip-width pole (hip joint attachment points on lTorso)
///   •   ↳ crossbar         — current hip height on posture pole
///   • Violet bar           — shoulder-width pole (shoulder joint attachment points on uTorso)
///   •   ↳ crossbar         — current shoulder height on posture pole
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
    private RigidBody3D                _uTorso;
    private RigidBody3D                _head;
    private BalanceController          _balance;
    private FootStepper                _stepper;

    // Shoulder/hip pole endpoints stored in torso-local space so they track
    // the torso rotation without inheriting noise from swinging limbs.
    private Vector3 _lShoulderLocal;
    private Vector3 _rShoulderLocal;
    private Vector3 _lHipLocal;
    private Vector3 _rHipLocal;

    public void Setup(
        IReadOnlyList<RigidBody3D> bodies,
        RigidBody3D                lTorso,
        RigidBody3D                uTorso,
        RigidBody3D                head,
        BalanceController          balance,
        FootStepper                stepper       = null,
        RigidBody3D                lShoulderBody = null,
        RigidBody3D                rShoulderBody = null,
        RigidBody3D                lHipBody      = null,
        RigidBody3D                rHipBody      = null)
    {
        _bodies  = bodies;
        _lTorso  = lTorso;
        _uTorso  = uTorso;
        _head    = head;
        _balance = balance;
        _stepper = stepper;

        // Bake attachment points into torso-local space so the poles track
        // the torso frame and stay immune to limb wobble.
        if (uTorso != null && lShoulderBody != null && rShoulderBody != null)
        {
            var inv = uTorso.GlobalTransform.AffineInverse();
            _lShoulderLocal = inv * lShoulderBody.GlobalPosition;
            _rShoulderLocal = inv * rShoulderBody.GlobalPosition;
        }
        if (lTorso != null && lHipBody != null && rHipBody != null)
        {
            var inv = lTorso.GlobalTransform.AffineInverse();
            _lHipLocal = inv * lHipBody.GlobalPosition;
            _rHipLocal = inv * rHipBody.GlobalPosition;
        }
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
        var comVel  = ComputeCoMVelocity();
        var groundY = ComputeGroundY();

        // ── Momentum arrow ────────────────────────────────────────────────────
        // XZ velocity projected to the ground plane — shows where the rig is heading
        // and grows/shrinks linearly with speed (0.2 m per m/s, min 0.05 m/s to draw).
        var velXZ = new Vector3(comVel.X, 0f, comVel.Z);
        if (velXZ.Length() > 0.05f)
        {
            Col(new Color(0.5f, 1f, 0.1f)); // lime — distinct from cyan anchor, orange posture, green feet
            DrawArrow(com, velXZ, velXZ.Length() * 0.2f);
        }

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

        // ── Hip / shoulder pole endpoints (hoisted so posture pole can use them) ─
        var haveHips = _lTorso != null && GodotObject.IsInstanceValid(_lTorso)
                       && _lHipLocal != Vector3.Zero && _rHipLocal != Vector3.Zero;
        var haveShoulders = _uTorso != null && GodotObject.IsInstanceValid(_uTorso)
                            && _lShoulderLocal != Vector3.Zero && _rShoulderLocal != Vector3.Zero;

        var lHip      = haveHips      ? _lTorso.GlobalTransform * _lHipLocal      : Vector3.Zero;
        var rHip      = haveHips      ? _lTorso.GlobalTransform * _rHipLocal      : Vector3.Zero;
        var lShoulder = haveShoulders ? _uTorso.GlobalTransform * _lShoulderLocal : Vector3.Zero;
        var rShoulder = haveShoulders ? _uTorso.GlobalTransform * _rShoulderLocal : Vector3.Zero;

        // ── Posture pole ──────────────────────────────────────────────────────
        // Orange vertical axis: the ideal alignment line rising from the foot
        // midpoint. A well-postured body keeps hips, spine, and head stacked
        // over this line. Deviations show what's pulling out of alignment.
        if (_stepper != null && _stepper.IsReady &&
            _lTorso != null && GodotObject.IsInstanceValid(_lTorso))
        {
            // Ideal base = midpoint of the two foot targets on the ground.
            var footMid  = (_stepper.LeftTarget + _stepper.RightTarget) * 0.5f;
            footMid.Y    = groundY;

            // Pole rises to estimated head height (2 m above ground is plenty).
            var poleTop  = new Vector3(footMid.X, groundY + 2f, footMid.Z);

            Col(new Color(1f, 0.5f, 0f)); // orange
            DrawLine(footMid, poleTop);

            // Actual landmark positions — sphere + line to the pole's nearest point.
            // Hip (lower torso) — red
            DrawLandmarkToPole(_lTorso, footMid, new Color(1f, 0.2f, 0.2f));

            // Chest (upper torso) — orange-yellow
            if (_uTorso != null && GodotObject.IsInstanceValid(_uTorso))
                DrawLandmarkToPole(_uTorso, footMid, new Color(1f, 0.8f, 0f));

            // Head — white
            if (_head != null && GodotObject.IsInstanceValid(_head))
                DrawLandmarkToPole(_head, footMid, Colors.White);

            // Facing axes for crossbar orientation — rotate with the character.
            // AnchorRight tracks yaw input via _anchorRestBasis; fall back to world
            // axes if no balance controller is present.
            var charRight   = _balance != null ? _balance.AnchorRight : Vector3.Right;
            var charForward = Vector3.Up.Cross(charRight).Normalized();

            // ── Desired hip height on posture pole ───────────────────────────
            // Horizontal crossbar at the current hip midpoint height — shows
            // where the hips sit on the ideal axis so lean/sag is immediately obvious.
            if (haveHips)
            {
                var hipY = new Vector3(footMid.X, (lHip.Y + rHip.Y) * 0.5f, footMid.Z);
                Col(new Color(0.27f, 0.51f, 0.93f)); // cornflower blue — matches hip pole
                DrawLine(hipY - charRight   * 0.12f, hipY + charRight   * 0.12f);
                DrawLine(hipY - charForward * 0.12f, hipY + charForward * 0.12f);
            }

            // ── Desired shoulder height on posture pole ───────────────────────
            // Same crossbar at shoulder midpoint height.
            if (haveShoulders)
            {
                var shoulderY = new Vector3(footMid.X, (lShoulder.Y + rShoulder.Y) * 0.5f, footMid.Z);
                Col(new Color(0.65f, 0.15f, 1f)); // violet — matches shoulder pole
                DrawLine(shoulderY - charRight   * 0.12f, shoulderY + charRight   * 0.12f);
                DrawLine(shoulderY - charForward * 0.12f, shoulderY + charForward * 0.12f);
            }
        }

        // ── Hip pole ──────────────────────────────────────────────────────────
        // Cornflower blue: spans the two hip attachment points baked onto lTorso.
        // Because endpoints are stored in lTorso-local space they stay anchored to
        // the spine and are unaffected by leg swing.
        if (haveHips)
        {
            Col(new Color(0.27f, 0.51f, 0.93f)); // cornflower blue
            DrawWireSphere(lHip, 0.03f, 6);
            DrawWireSphere(rHip, 0.03f, 6);
            DrawLine(lHip, rHip);
        }

        // ── Shoulder pole ─────────────────────────────────────────────────────
        // Violet: spans the two shoulder attachment points baked onto uTorso.
        // Same torso-local anchoring keeps this stable despite arm movement.
        if (haveShoulders)
        {
            Col(new Color(0.65f, 0.15f, 1f)); // violet
            DrawWireSphere(lShoulder, 0.03f, 6);
            DrawWireSphere(rShoulder, 0.03f, 6);
            DrawLine(lShoulder, rShoulder);
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

    private Vector3 ComputeCoMVelocity()
    {
        var vel   = Vector3.Zero;
        float tot = 0f;
        foreach (var b in _bodies)
        {
            if (!IsInstanceValid(b)) continue;
            vel += b.LinearVelocity * b.Mass;
            tot += b.Mass;
        }
        return tot > 0f ? vel / tot : Vector3.Zero;
    }

    /// Draws a line from <paramref name="origin"/> in <paramref name="dir"/> scaled to
    /// <paramref name="length"/>, with a two-line arrowhead at the tip.
    private void DrawArrow(Vector3 origin, Vector3 dir, float length)
    {
        if (dir.LengthSquared() < 0.0001f) return;
        var d   = dir.Normalized();
        var tip = origin + d * length;
        DrawLine(origin, tip);

        // Arrowhead: two lines angled 35° back from the tip in the XZ plane.
        var perp      = new Vector3(-d.Z, 0f, d.X); // 90° rotation in XZ
        var headLen   = Mathf.Min(length * 0.35f, 0.18f);
        var headBack  = -d * headLen * Mathf.Cos(Mathf.DegToRad(35f));
        var headSpread = perp * headLen * Mathf.Sin(Mathf.DegToRad(35f));
        DrawLine(tip, tip + headBack + headSpread);
        DrawLine(tip, tip + headBack - headSpread);
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

    /// Draws a small sphere at the body's position and a horizontal line to the
    /// nearest point on the ideal vertical pole, showing XZ deviation.
    private void DrawLandmarkToPole(RigidBody3D body, Vector3 poleBase, Color c)
    {
        var pos      = body.GlobalPosition;
        var onPole   = new Vector3(poleBase.X, pos.Y, poleBase.Z);
        Col(c);
        DrawWireSphere(pos, 0.05f, 8);
        DrawLine(pos, onPole); // horizontal deviation from ideal axis
    }

    private void DrawCross(Vector3 center, float size)
    {
        DrawLine(center - Vector3.Right   * size, center + Vector3.Right   * size);
        DrawLine(center - Vector3.Up      * size, center + Vector3.Up      * size);
        DrawLine(center - Vector3.Forward * size, center + Vector3.Forward * size);
    }
}
