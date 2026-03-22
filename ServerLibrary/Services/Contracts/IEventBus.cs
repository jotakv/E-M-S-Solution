namespace ServerLibrary.Services.Contracts;

/// <summary>
/// Publish a domain event payload to the message broker.
/// Implementations must be fire-and-forget and must not throw;
/// any broker unavailability is handled internally with a warning log.
/// </summary>
public interface IEventBus
{
    /// <param name="routingKey">
    ///   Dot-separated routing key, e.g. "ems.employee.created".
    ///   Consumers bind queues to patterns like "ems.employee.*".
    /// </param>
    /// <param name="payload">JSON-serialised event payload.</param>
    void Publish(string routingKey, string payload);
}
