using Godot;
using Godot.Collections;
using blendporter.definition;

namespace blendporter.dispatcher.worker;

public static class SceneCreator
{
    private const string SceneExtension = ".tscn";
    public static bool Create(Node node, string pathString)
    {
        if (!DirAccess.DirExistsAbsolute(pathString))
            return false;
        SetOwnerRecursive(node, node);
        // Reset node position to 0, 0, 0
        var resetPositionKey = (Variant)new StringName(Properties.ResetPosition);
        var extrasDictionary = (Variant)new Dictionary{ [resetPositionKey] = Properties.OriginPosition};
        node.SetMeta(Properties.BlenderMetaKey, extrasDictionary);
        if (!PropertyApplicator.Apply(node, Properties.BlenderMetaKey))
            PluginLogger.Log(LogLevel.Error,$"Node re-centering for \"{node.Name}{SceneExtension}\" failed");
        // Pack and save file
        var packedNode = new PackedScene();
        packedNode.Pack(node);
        var filePath = $"{pathString}/{node.Name}{SceneExtension}";
        // Iterate through numbering filenames if you have to
        var i = 1;
        while (FileAccess.FileExists(filePath))
            filePath = $"{pathString}/{node.Name}-{i++}{SceneExtension}";
        var error = ResourceSaver.Save(packedNode, filePath);
        if (error == Error.Ok)
            return true;
        PluginLogger.Log(LogLevel.Error, $"Scene could not be created. Failed with error \"{error.ToString()}\"");
        return false;
    }

    private static void SetOwnerRecursive(Node node, Node owner)
    {
        if(node != owner)
            node.Owner = owner;
        foreach (var child in node.GetChildren())
            SetOwnerRecursive(child, owner);
    }
}