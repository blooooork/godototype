using Godot;
using godotconsole;
using godototype.objects;
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
    }

    public override void _ExitTree()
    {
        ConsoleManager.Unregister("spawn");
    }
}
