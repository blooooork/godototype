using Godot;
using System.Linq;
using blendporter.dispatcher.worker;

namespace blendporter.dispatcher;

public class MetaPropertyDispatcher: IDispatcher
{
    private static readonly ValidationWorker ValidationWorker = new ();
    private static readonly PropertyWorker PropertyWorker = new ();
    public bool Dispatch(Node node)
    {
        // Recursively process all child nodes
        var children = new Godot.Collections.Array<Node>();
        foreach (var child in node.GetChildren())
            children.Add(child);
        // Attempt to apply properties
        var successCount = ApplyProperty(node) ? 1 : 0;
        successCount += children.Sum(c => ApplyProperty(c) ? 1 : 0);
        GD.Print($"{successCount} nodes of \"{node.Name}\" have been updated with meta properties");
        return successCount > 0;
    }

    private static bool ApplyProperty(Node node)
    {
        if (!ValidationWorker.Work(node, ValidationWorker.Type.MetaData))
            return false;
        // Attempt to apply properties from validated meta dictionary
        var metaList = node.GetMetaList();
        return metaList.Select(meta => PropertyWorker.Work(node, meta))
            // Collect the outputs to a list and see if any were true
            .ToList()
            .Any(x => x);
    }

    public void Reset()
    {
        // Nothing to reset in this one
    }
}
