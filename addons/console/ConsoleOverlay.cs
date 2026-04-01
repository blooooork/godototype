using Godot;
using godototype.input;
using System.Collections.Generic;

namespace godotconsole;

public partial class ConsoleOverlay : CanvasLayer
{
    private RichTextLabel _output;
    private LineEdit _input;
    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    public override void _Ready()
    {
        Layer = 128;
        Visible = false;
        BuildUi();

        ConsoleManager.OnOutput += AppendLine;
        ConsoleManager.Register("help", "help — list all commands", _ =>
        {
            foreach (var cmd in ConsoleManager.Commands.Values)
                ConsoleManager.Print(cmd.Usage);
        });
    }

    private void BuildUi()
    {
        var panel = new Panel();
        panel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        panel.CustomMinimumSize = new Vector2(0, 250);
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        panel.AddChild(vbox);

        _output = new RichTextLabel();
        _output.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _output.BbcodeEnabled = true;
        _output.ScrollFollowing = true;
        vbox.AddChild(_output);

        _input = new LineEdit();
        _input.PlaceholderText = "Enter command...";
        _input.TextSubmitted += OnSubmit;
        vbox.AddChild(_input);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

        if (key.Keycode == Key.Escape && key.ShiftPressed)
        {
            GetViewport().SetInputAsHandled();
            Toggle();
            return;
        }

        if (!Visible) return;

        if (key.Keycode == Key.Up)
        {
            _historyIndex = Mathf.Min(_historyIndex + 1, _history.Count - 1);
            if (_historyIndex >= 0) _input.Text = _history[_historyIndex];
            _input.CaretColumn = _input.Text.Length;
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.Down)
        {
            _historyIndex = Mathf.Max(_historyIndex - 1, -1);
            _input.Text = _historyIndex >= 0 ? _history[_historyIndex] : "";
            _input.CaretColumn = _input.Text.Length;
            GetViewport().SetInputAsHandled();
        }
    }

    private void Toggle()
    {
        Visible = !Visible;
        InputManager.Suppressed = Visible;
        if (Visible)
        {
            _input.GrabFocus();
            _input.Clear();
            _historyIndex = -1;
        }
    }

    private void OnSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _history.Insert(0, text);
        _historyIndex = -1;
        AppendLine($"[color=gray]> {text}[/color]");
        _input.Clear();
        ConsoleManager.Execute(text);
    }

    private void AppendLine(string text) => _output.AppendText($"\n{text}");

    public override void _ExitTree()
    {
        ConsoleManager.OnOutput -= AppendLine;
    }
}
