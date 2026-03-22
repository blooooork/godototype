using Godot;
using blendporter.definition;

namespace blendporter.dispatcher.worker;

public class ValidationWorker : IWorker
{
    #nullable enable
    public bool Work(Node node, object? details)
    {
        var metaList = node.GetMetaList();
        if (metaList == null || metaList.Count == 0)
        {
            GD.Print($"{node.Name} had an empty meta list");
            return false;
        }
        // Validate node type is defined
        if (!DefinitionRegistry.IsTypeDefined(node.GetType()))
        {
            GD.Print($"{node.Name}'s type definition is not defined");
            return false;
        }
        return true;
    }
    #nullable disable
}