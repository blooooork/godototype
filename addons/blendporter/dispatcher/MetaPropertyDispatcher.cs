using Godot;
using System.Linq;
using blendporter.definition;
using blendporter.dispatcher.worker;

namespace blendporter.dispatcher;

public static class MetaPropertyDispatcher
{
    public static void ApplyProperties(Node node)
    {
        // Recursively process all child nodes
        var children = new Godot.Collections.Array<Node>();
        foreach (var child in node.GetChildren())
            children.Add(child);
        // Attempt to apply properties
        var successCount = ValidateAndApply(node) ? 1 : 0;
        successCount += children.Sum(c => ValidateAndApply(c) ? 1 : 0);
        var successLog = $"{successCount} nodes of \"{node.Name}\" have been updated with meta properties";
        PluginLogger.Log(LogLevel.Debug, successLog);
    }

    private static bool ValidateAndApply(Node node)
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
}
