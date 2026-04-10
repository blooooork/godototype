using Godot;
using godototype.input;
using System.Collections.Generic;
using System.Text;

namespace godotconsole;

public partial class ConsoleOverlay : CanvasLayer
{
	[Export] public bool FocusedStats { get; set; }

	private Panel _consolePanel;
	private RichTextLabel _output;
	private LineEdit _input;
	private Label _statsLabel;
	private readonly List<string> _history = new();
	private int _historyIndex = -1;

	public override void _Ready()
	{
		Layer = 128;
		BuildUi();

		ConsoleManager.OnOutput += AppendLine;
		ConsoleManager.Register("help", "help — list all commands", _ =>
		{
			foreach (var cmd in ConsoleManager.Commands.Values)
				ConsoleManager.Print(cmd.Usage);
		});
		ConsoleManager.Register("stats", "stats — toggle stats overlay", _ => FocusedStats = !FocusedStats);
	}

	public override void _Process(double delta)
	{
		_statsLabel.Visible = FocusedStats;
		if (!FocusedStats) return;

		var sb = new StringBuilder();
		foreach (var (name, value) in StatsManager.GetStats(StatsManager.FocusedEntity))
			sb.AppendLine($"{name}: {value}");
		_statsLabel.Text = sb.Length > 0 ? sb.ToString().TrimEnd() : "(no focused entity)";
	}

	private void BuildUi()
	{
		_consolePanel = new Panel();
		_consolePanel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_consolePanel.CustomMinimumSize = new Vector2(0, 250);
		_consolePanel.Visible = false;
		AddChild(_consolePanel);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_consolePanel.AddChild(vbox);

		_output = new RichTextLabel();
		_output.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		_output.BbcodeEnabled = true;
		_output.ScrollFollowing = true;
		vbox.AddChild(_output);

		_input = new LineEdit();
		_input.PlaceholderText = "Enter command...";
		_input.TextSubmitted += OnSubmit;
		vbox.AddChild(_input);

		_statsLabel = new Label();
		_statsLabel.Position = new Vector2(8, 8);
		_statsLabel.Size = new Vector2(300, 400);
		_statsLabel.Visible = false;
		AddChild(_statsLabel);
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

		if (!_consolePanel.Visible) return;

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
		_consolePanel.Visible = !_consolePanel.Visible;
		InputManager.Suppressed = _consolePanel.Visible;
		if (_consolePanel.Visible)
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
