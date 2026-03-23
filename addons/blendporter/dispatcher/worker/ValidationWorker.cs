using Godot;
using System.Linq;

using blendporter.definition;

namespace blendporter.dispatcher.worker;

public class ValidationWorker : IWorker
{
    public enum Type
    {
        MetaData,
        DictionaryName
    }
    
    #nullable enable
    public bool Work(object incomingObject, object? details)
    {
        if (details is not Type detailsType)
            return false;
        switch (detailsType)
        {
            case Type.DictionaryName:
                if (incomingObject is not StringName name)
                    return false;
                return Properties.MetaKeys.Contains(name);
            case Type.MetaData:
                if (incomingObject is not Node3D node)
                    return false;
                var metaList = node.GetMetaList();
                if (metaList == null || metaList.Count == 0)
                {
                    GD.Print($"{node.Name} had an empty meta list");
                    return false;
                }
                // Validate node type is defined
                if (!Properties.TypeDictionary.ContainsKey(node.GetType()))
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