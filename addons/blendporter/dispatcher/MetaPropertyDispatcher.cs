using Godot;
using System.Linq;
using blendporter.definition;
using blendporter.dispatcher.worker;

namespace blendporter.dispatcher;

public class MetaPropertyDispatcher: IDispatcher
{
    public bool Dispatch(object incomingObject)
    {
        if (incomingObject is not Node3D node)
            return false;
        // Recursively process all child nodes
        var children = new Godot.Collections.Array<Node3D>();
        foreach (var child in node.GetChildren())
            children.Add((Node3D)child);
        // Attempt to apply properties
        var successCount = ApplyProperty(node) ? 1 : 0;
        successCount += children.Sum(c => ApplyProperty(c) ? 1 : 0);
        var successLog = $"{successCount} nodes of \"{node.Name}\" have been updated with meta properties";
        PluginLogger.Log(LogLevel.Debug, successLog);
        return successCount > 0;
    }

    private static bool ApplyProperty(Node3D node)
    {
        if (!ValidationWorker.Validate(node, ValidationWorker.Type.MetaData))
            return false;
        // Attempt to apply properties from validated meta dictionary
        var metaList = node.GetMetaList();
        return metaList.Select(meta => PropertyWorker.ApplyDictionaryProperties(node, meta))
            // Collect the outputs to a list and see if any were true
            .ToList()
            .Any(x => x);
    }

    public void Reset()
    {
        // Nothing to reset in this one
    }
}
