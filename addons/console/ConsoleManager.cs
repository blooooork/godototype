using System;
using System.Collections.Generic;

namespace godotconsole;

public static class ConsoleManager
{
    private static readonly Dictionary<string, ConsoleCommand> _commands =
        new(StringComparer.OrdinalIgnoreCase);

    internal static event Action<string> OnOutput;

    public static IReadOnlyDictionary<string, ConsoleCommand> Commands => _commands;

    public static void Register(string name, string usage, Action<string[]> handler)
        => _commands[name] = new ConsoleCommand(name, usage, handler);

    public static void Unregister(string name)
        => _commands.Remove(name);

    public static void Execute(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var name = parts[0];
        var args = parts[1..];

        if (!_commands.TryGetValue(name, out var command))
        {
            Print($"Unknown command '{name}'. Type 'help' for a list of commands.");
            return;
        }

        command.Handler(args);
    }

    public static void Print(string message) => OnOutput?.Invoke(message);
}
