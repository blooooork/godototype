using Godot;
using godotconsole;
using godototype.objects;
using godototype.world;
using System;
using System.Linq;

public partial class Gym : Node3D
{
    public override void _EnterTree()
    {
        ConsoleManager.Register("spawn", "spawn <point|all> <type>", args =>
        {
            if (args.Length < 2)
            {
                ConsoleManager.Print("Usage: spawn <point|all> <type>");
                return;
            }

            var pointName = args[0];
            var typeName = args[1];

            var allPoints = FindChildren("*").OfType<SpawnPoint>();
            var targets = pointName.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? allPoints
                : allPoints.Where(p => ((string)p.Name).Equals(pointName, StringComparison.OrdinalIgnoreCase));

            foreach (var point in targets)
                point.TrySpawn(typeName);
        });

        ConsoleManager.Register("reset", "reset [name] — reset all spawned objects, or a specific one by name", args =>
        {
            if (args.Length > 0)
            {
                var name = args[0];
                if (SpawnRegistry.ResetByName(name))
                    ConsoleManager.Print($"Reset '{name}'.");
                else
                    ConsoleManager.Print($"No spawned object named '{name}'.");
                return;
            }
            SpawnRegistry.ResetAll();
            ConsoleManager.Print($"Reset {SpawnRegistry.Count} spawned object(s).");
        });
    }

    public override void _ExitTree()
    {
        ConsoleManager.Unregister("spawn");
        ConsoleManager.Unregister("reset");
    }
}
