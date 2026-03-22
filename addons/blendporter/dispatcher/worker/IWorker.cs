using Godot;

namespace blendporter.dispatcher.worker;

public interface IWorker
{
    #nullable enable
    public bool Work(Node node, object? details);
}