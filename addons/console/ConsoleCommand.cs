using System;

namespace godotconsole;

public sealed class ConsoleCommand
{
    public string Name { get; }
    public string Usage { get; }
    public Action<string[]> Handler { get; }

    public ConsoleCommand(string name, string usage, Action<string[]> handler)
    {
        Name = name;
        Usage = usage;
        Handler = handler;
    }
}
