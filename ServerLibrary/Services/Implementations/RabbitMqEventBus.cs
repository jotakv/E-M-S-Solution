using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ServerLibrary.Helpers;
using ServerLibrary.Services.Contracts;

namespace ServerLibrary.Services.Implementations;

/// <summary>
/// Publishes domain events to RabbitMQ as persistent JSON messages.
/// Failures are swallowed and logged — the main business flow is never blocked.
/// </summary>
public sealed class RabbitMqEventBus : IEventBus, IDisposable
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public RabbitMqEventBus(
        IOptions<RabbitMqSettings> settings,
        ILogger<RabbitMqEventBus> logger)
    {
        _settings = settings.Value;
        _logger   = logger;
        TryConnect();
    }

    public void Publish(string routingKey, object payload)
    {
        try
        {
            EnsureChannel();
            var body = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(payload,
                    new JsonSerializerOptions { WriteIndented = false }));

            var props = _channel!.CreateBasicProperties();
            props.Persistent  = true;
            props.ContentType = "application/json";
            props.Timestamp   = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            props.Headers     = new Dictionary<string, object>
            {
                ["routing-key"] = routingKey,
                ["source"]      = "EMS-Server"
            };

            _channel.BasicPublish(
                exchange:        _settings.ExchangeName,
                routingKey:      routingKey,
                basicProperties: props,
                body:            body);

            _logger.LogDebug(
                "RabbitMQ event published — RoutingKey: {RoutingKey}, PayloadSize: {Bytes}B",
                routingKey, body.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "RabbitMQ publish failed — RoutingKey: {RoutingKey}. " +
                "Event dropped, business operation continues.",
                routingKey);
        }
    }

    private void TryConnect()
    {
        try
        {
            lock (_lock)
            {
                var factory = new ConnectionFactory
                {
                    HostName                 = _settings.HostName,
                    Port                     = _settings.Port,
                    UserName                 = _settings.UserName,
                    Password                 = _settings.Password,
                    VirtualHost              = _settings.VirtualHost,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval  = TimeSpan.FromSeconds(10)
                };
                _connection = factory.CreateConnection("EMS-Server");
                _channel    = _connection.CreateModel();

                _channel.ExchangeDeclare(
                    exchange:   _settings.ExchangeName,
                    type:       _settings.ExchangeType,
                    durable:    true,
                    autoDelete: false);

                _channel.QueueDeclare(
                    queue:      _settings.QueueName,
                    durable:    true,
                    exclusive:  false,
                    autoDelete: false);

                _channel.QueueBind(
                    queue:      _settings.QueueName,
                    exchange:   _settings.ExchangeName,
                    routingKey: $"{_settings.RoutingKeyPrefix}.#");

                _logger.LogInformation(
                    "RabbitMQ connected — Host: {Host}:{Port}, Exchange: {Exchange}, Queue: {Queue}",
                    _settings.HostName, _settings.Port,
                    _settings.ExchangeName, _settings.QueueName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "RabbitMQ connection failed on startup — event publishing disabled. " +
                "Application will continue without RabbitMQ.");
        }
    }

    private void EnsureChannel()
    {
        if (_channel is { IsOpen: true }) return;
        lock (_lock)
        {
            if (_channel is { IsOpen: true }) return;
            _logger.LogWarning("RabbitMQ channel closed — attempting reconnect.");
            TryConnect();
        }
    }

    public void Dispose()
    {
        try { _channel?.Close(); _channel?.Dispose(); } catch { /* ignore */ }
        try { _connection?.Close(); _connection?.Dispose(); } catch { /* ignore */ }
    }
}
