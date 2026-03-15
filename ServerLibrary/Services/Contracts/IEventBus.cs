namespace ServerLibrary.Services.Contracts;

/// <summary>
/// Fire-and-forget event publisher. Never throws — failures are logged only.
/// </summary>
public interface IEventBus
{
    void Publish(string routingKey, object payload);
}
