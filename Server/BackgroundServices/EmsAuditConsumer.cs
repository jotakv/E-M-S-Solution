using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ServerLibrary.Helpers;

namespace Server.BackgroundServices;

/// <summary>
/// Consumes EMS audit events from RabbitMQ. Extend ProcessMessage to persist, alert, etc.
/// </summary>
public sealed class EmsAuditConsumer : BackgroundService
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<EmsAuditConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public EmsAuditConsumer(
        IOptions<RabbitMqSettings> settings,
        ILogger<EmsAuditConsumer> logger)
    {
        _settings = settings.Value;
        _logger   = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();
        try
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

            _connection = factory.CreateConnection("EMS-AuditConsumer");
            _channel    = _connection.CreateModel();
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (_, ea) =>
            {
                try
                {
                    var body = Encoding.UTF8.GetString(ea.Body.Span);
                    ProcessMessage(ea.RoutingKey, body);
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to process RabbitMQ message — RoutingKey: {RoutingKey}. Nacking.",
                        ea.RoutingKey);
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(
                queue:    _settings.QueueName,
                autoAck:  false,
                consumer: consumer);

            _logger.LogInformation(
                "EmsAuditConsumer started — listening on queue: {Queue}", _settings.QueueName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "EmsAuditConsumer could not start — RabbitMQ unavailable.");
        }
        return Task.CompletedTask;
    }

    private void ProcessMessage(string routingKey, string body)
    {
        _logger.LogInformation(
            "EMS audit event received — RoutingKey: {RoutingKey}, Body: {Body}",
            routingKey, body);
    }

    public override void Dispose()
    {
        try { _channel?.Close(); _channel?.Dispose(); } catch { /* ignore */ }
        try { _connection?.Close(); _connection?.Dispose(); } catch { /* ignore */ }
        base.Dispose();
    }
}
