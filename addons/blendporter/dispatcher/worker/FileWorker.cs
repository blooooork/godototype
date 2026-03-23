using Godot;
using Godot.Collections;
using blendporter.definition;

namespace blendporter.dispatcher.worker;

public class FileWorker : IWorker
{
    private const string SceneExtension = ".tscn";
    private static readonly PropertyWorker PropertyWorker = new();
    #nullable enable
    public bool Work(object incomingObject, object? details)
    {
        if (incomingObject is not Node3D node)
            return false;
        if (details is not string detailString || !DirAccess.DirExistsAbsolute(detailString))
            return false;
        SetOwnerRecursive(node, node);
        // Reset node position to 0, 0, 0
        var resetPositionKey = (Variant)new StringName(Names.ResetPosition);
        var extrasDictionary = (Variant)new Dictionary{ [resetPositionKey] = Defaults.Position};
        node.SetMeta(Names.Extras, extrasDictionary);
        if (!PropertyWorker.Work(node, Names.Extras))
            GD.Print($"Node re-centering for \"{node.Name}{SceneExtension}\" failed");
        // Pack and save file
        var packedNode = new PackedScene();
        packedNode.Pack(node);
        var filePath = $"{details}/{node.Name}{SceneExtension}";
        // Iterate through numbering filenames if you have to
        var i = 1;
        while (FileAccess.FileExists(filePath))
            filePath = $"{details}/{node.Name}-{i++}{SceneExtension}";
        var error = ResourceSaver.Save(packedNode, filePath);
        if (error == Error.Ok)
            return true;
        GD.PrintErr($"Scene could not be created. Failed with error \"{error.ToString()}\"");
        return false;
    }
    #nullable disable

    private static void SetOwnerRecursive(Node node, Node owner)
    {
        if(node != owner)
            node.Owner = owner;
        foreach (var child in node.GetChildren())
            SetOwnerRecursive(child, owner);
    }
}