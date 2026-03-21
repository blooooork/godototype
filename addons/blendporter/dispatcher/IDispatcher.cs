using Godot;

namespace blendporter.dispatcher;

public interface IDispatcher
{
    public bool Dispatch(Node node);

    public void Reset();
}