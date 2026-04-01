using Godot;
using System;
using System.Collections.Generic;

namespace godototype.input;

/// <summary>
/// Autoload input manager. Single tick loop, event-driven consumers.
///
/// Polling (still available for systems that already tick):
///   InputManager.IsPressed("jump")
///   InputManager.JustPressed("attack")
///
/// Event-driven (no tick needed on consumer side):
///   InputManager.Subscribe("jump", onPressed: OnJump);
///   InputManager.Unsubscribe("jump", onPressed: OnJump);
/// </summary>
public partial class InputManager : Node
{
    public static InputManager Instance { get; private set; }
    public static bool Suppressed { get; set; }

    private static readonly Dictionary<string, ActionState> _actions = new();
    private static readonly Dictionary<string, List<Action<string>>> _onJustPressed = new();
    private static readonly Dictionary<string, List<Action<string>>> _onJustReleased = new();
    private static readonly Dictionary<string, List<Action<string>>> _onPressed = new();
    private static readonly Dictionary<string, List<Action<string>>> _onReleased = new();

    private struct ActionState
    {
        public bool Pressed;
        public bool JustPressed;
        public bool JustReleased;
        public float Strength;
    }

    public override void _EnterTree()
    {
        Instance = this;
        ProcessPriority = -100;
    }

    public override void _Process(double delta)
    {
        if (Suppressed) return;

        foreach (var action in _actions.Keys)
        {
            _actions[action] = new ActionState
            {
                Pressed = Input.IsActionPressed(action),
                JustPressed = Input.IsActionJustPressed(action),
                JustReleased = Input.IsActionJustReleased(action),
                Strength = Input.GetActionStrength(action),
            };
        }

        foreach (var (action, state) in _actions)
        {
            if (state.JustPressed && _onJustPressed.TryGetValue(action, out var jpList))
                for (var i = jpList.Count - 1; i >= 0; i--) jpList[i](action);
            if (state.JustReleased && _onJustReleased.TryGetValue(action, out var jrList))
                for (var i = jrList.Count - 1; i >= 0; i--) jrList[i](action);
            if (state.Pressed && _onPressed.TryGetValue(action, out var pList))
                for (var i = pList.Count - 1; i >= 0; i--) pList[i](action);
            if (!state.Pressed && _onReleased.TryGetValue(action, out var rList))
                for (var i = rList.Count - 1; i >= 0; i--) rList[i](action);
        }
    }

    // --- Registration & Subscription ---

    public static void Register(params string[] actions)
    {
        foreach (var a in actions)
        {
            if (!InputMap.HasAction(a))
            {
                GD.PushError($"InputManager: '{a}' is not defined in the Input Map.");
                continue;
            }
            _actions.TryAdd(a, default);
        }
    }

    public static void Subscribe(
        string action,
        Action<string> onJustPressed = null,
        Action<string> onJustReleased = null,
        Action<string> onPressed = null,
        Action<string> onReleased = null)
    {
        if (onJustPressed == null && onJustReleased == null && onPressed == null && onReleased == null)
        {
            GD.PushWarning($"InputManager: Subscribe('{action}') called with no callbacks.");
            return;
        }
        if (!InputMap.HasAction(action))
        {
            GD.PushError($"InputManager: '{action}' is not defined in the Input Map.");
            return;
        }
        Add(_onJustPressed, action, onJustPressed);
        Add(_onJustReleased, action, onJustReleased);
        Add(_onPressed, action, onPressed);
        Add(_onReleased, action, onReleased);
        Register(action);
    }

    public static void Unsubscribe(
        string action,
        Action<string> onJustPressed = null,
        Action<string> onJustReleased = null,
        Action<string> onPressed = null,
        Action<string> onReleased = null)
    {
        Remove(_onJustPressed, action, onJustPressed);
        Remove(_onJustReleased, action, onJustReleased);
        Remove(_onPressed, action, onPressed);
        Remove(_onReleased, action, onReleased);
    }

    private static void Add(Dictionary<string, List<Action<string>>> dict, string action, Action<string> cb)
    {
        if (cb == null) return;
        if (!dict.ContainsKey(action)) dict[action] = new();
        dict[action].Add(cb);
    }

    private static void Remove(Dictionary<string, List<Action<string>>> dict, string action, Action<string> cb)
    {
        if (cb != null && dict.TryGetValue(action, out var list)) list.Remove(cb);
    }

    // --- Polling (for systems that already tick) ---

    public static bool IsPressed(string action) =>
        _actions.TryGetValue(action, out var s) && s.Pressed;

    public static bool JustPressed(string action) =>
        _actions.TryGetValue(action, out var s) && s.JustPressed;

    public static bool JustReleased(string action) =>
        _actions.TryGetValue(action, out var s) && s.JustReleased;

    public static float GetStrength(string action) =>
        _actions.TryGetValue(action, out var s) ? s.Strength : 0f;

    public static float GetAxis(string negative, string positive) =>
        GetStrength(positive) - GetStrength(negative);

    public static Vector2 GetVector(string left, string right, string up, string down)
    {
        var v = new Vector2(GetAxis(left, right), GetAxis(up, down));
        return v.LengthSquared() > 1f ? v.Normalized() : v;
    }
}