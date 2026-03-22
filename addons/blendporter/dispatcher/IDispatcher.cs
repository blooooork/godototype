using Godot;

namespace blendporter.dispatcher;

public interface IDispatcher
{
    public bool Dispatch(object incomingObject);

    public void Reset();
}