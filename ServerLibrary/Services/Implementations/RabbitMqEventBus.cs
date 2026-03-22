using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ServerLibrary.Helpers;
using ServerLibrary.Services.Contracts;
using System.Text;

namespace ServerLibrary.Services.Implementations;

/// <summary>
/// Fire-and-forget RabbitMQ publisher that connects to CloudAMQP.
/// If the broker is unavailable the error is logged as a Warning and
/// the application continues normally — events are silently dropped.
/// </summary>
public sealed class RabbitMqEventBus : IEventBus, IDisposable
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private IConnection? _connection;
    private IModel?      _channel;
    private bool         _available;

    public RabbitMqEventBus(
        IOptions<RabbitMqSettings> settings,
        ILogger<RabbitMqEventBus> logger)
    {
        _settings = settings.Value;
        _logger   = logger;
        TryConnect();
    }

    // ── Connection ────────────────────────────────────────────────────────────

    private void TryConnect()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName    = _settings.HostName,
                Port        = _settings.Port,
                UserName    = _settings.UserName,
                Password    = _settings.Password,
                VirtualHost = _settings.VirtualHost,
                Ssl         = new SslOption
                {
                    Enabled    = _settings.Port == 5671,
                    ServerName = _settings.HostName
                },
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval  = TimeSpan.FromSeconds(10)
            };

            _connection = factory.CreateConnection("EMS-Server");
            _channel    = _connection.CreateModel();

            // Declare durable topic exchange (idempotent — safe to call on every startup)
            _channel.ExchangeDeclare(
                exchange:    _settings.ExchangeName,
                type:        _settings.ExchangeType,
                durable:     true,
                autoDelete:  false);

            _available = true;

            _logger.LogInformation(
                "RabbitMQ connected — Host: {Host}:{Port}, VHost: {VHost}, Exchange: {Exchange}",
                _settings.HostName, _settings.Port, _settings.VirtualHost, _settings.ExchangeName);
        }
        catch (Exception ex)
        {
            _available = false;
            _logger.LogWarning(ex,
                "RabbitMQ unavailable — Host: {Host}:{Port}. Events will be dropped until broker recovers.",
                _settings.HostName, _settings.Port);
        }
    }

    // ── Publish ───────────────────────────────────────────────────────────────

    public void Publish(string routingKey, string payload)
    {
        if (!_available || _channel is null || !_channel.IsOpen)
        {
            _logger.LogWarning(
                "RabbitMQ publish skipped — broker unavailable. RoutingKey: {RoutingKey}", routingKey);
            return;
        }

        try
        {
            var body = Encoding.UTF8.GetBytes(payload);

            var props = _channel.CreateBasicProperties();
            props.Persistent    = true;
            props.ContentType   = "application/json";
            props.Timestamp     = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            props.MessageId     = Guid.NewGuid().ToString();

            _channel.BasicPublish(
                exchange:   _settings.ExchangeName,
                routingKey: routingKey,
                basicProperties: props,
                body:       body);

            _logger.LogInformation(
                "RabbitMQ event published — RoutingKey: {RoutingKey}, PayloadBytes: {Bytes}, MessageId: {MessageId}",
                routingKey, body.Length, props.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "RabbitMQ publish failed — RoutingKey: {RoutingKey}. Event dropped.", routingKey);
        }
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        try { _channel?.Close();    } catch { /* ignore */ }
        try { _connection?.Close(); } catch { /* ignore */ }
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
