namespace blendporter.dispatcher.worker;

public interface IWorker
{
    #nullable enable
    public bool Work(object incomingObject, object? details);
}