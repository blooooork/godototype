using blendporter.definition;
using Godot;
using Godot.Collections;

namespace blendporter.dispatcher;

public class SceneDispatcher : IDispatcher
{
    private const string SceneExtension = ".tscn";
    public string OutputPath {get; set;}
    private readonly MetaPropertyDispatcher _metaPropertyDispatcher = new ();

    public bool Dispatch(Node node)
    {
        if (OutputPath is null || !DirAccess.DirExistsAbsolute(OutputPath))
            return false;
        SetOwnerRecursive(node, node);
        // Reset node position to 0, 0, 0
        var resetPositionKey = (Variant)new StringName(CustomNames.ResetPosition);
        var extrasDictionary = (Variant)new Dictionary{ [resetPositionKey] = DefaultDefinitions.BlankVector3};
        node.SetMeta(DictionaryNames.Extras, extrasDictionary);
        if (!_metaPropertyDispatcher.Dispatch(node))
            GD.Print($"Node re-centering for \"{node.Name}{SceneExtension}\" failed");
        // Pack and save file
        var packedNode = new PackedScene();
        packedNode.Pack(node);
        var filePath = $"{OutputPath}/{node.Name}{SceneExtension}";
        var i = 1;
        while (FileAccess.FileExists(filePath))
            filePath = $"{OutputPath}/{node.Name}-{i++}{SceneExtension}";
        var error = ResourceSaver.Save(packedNode, filePath);
        if (error == Error.Ok)
            return true;
        GD.PushError($"Scene could not be created. Failed with error \"{error.ToString()}\"");
        return false;
    }

    private static void SetOwnerRecursive(Node node, Node owner)
    {
        if(node != owner)
            node.Owner = owner;
        foreach (var child in node.GetChildren())
            SetOwnerRecursive(child, owner);
    }

    public void Reset()
    {
        OutputPath = null;
    }
}