using Godot;
using blendporter.definition;

namespace blendporter.dispatcher.worker;

public class ValidationWorker : IWorker
{
    private enum Type
    {
        MetaList
    }
    
    #nullable enable
    public bool Work(Node node, object? details)
    {
        if (details is not Type detailsType)
            return false;

        switch (detailsType)
        {
            case Type.MetaList:
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
            default:
                return false;
        }
    }
    #nullable disable
}