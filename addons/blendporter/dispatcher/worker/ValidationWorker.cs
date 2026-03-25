using Godot;
using System.Linq;

using blendporter.definition;

namespace blendporter.dispatcher.worker;

public static class ValidationWorker
{
    public enum Type
    {
        MetaData,
        DictionaryName
    }
    
    public static bool Validate(object incomingObject, Type validationType)
    {
        switch (validationType)
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
                    PluginLogger.Log(LogLevel.Debug, $"{node.Name} had an empty meta list");
                    return false;
                }
                // Validate node type is defined
                if (!Properties.TypeDictionary.ContainsKey(node.GetType()))
                {
                    PluginLogger.Log(LogLevel.Debug, $"{node.Name}'s type definition is not defined");
                    return false;
                }
                return true;
            default:
                return false;
        }
    }
}