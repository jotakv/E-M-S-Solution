using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ServerLibrary.Data;
using ServerLibrary.Helpers;
using System.Text;
using System.Text.Json;

namespace Server.BackgroundServices;

/// <summary>
/// Background consumer that listens to the ems.audit queue, deserialises every
/// AuditEvent message, and persists it to the AuditLogs table via EF Core.
/// Starts only when RabbitMQ is reachable; exits gracefully if not.
/// Uses IServiceScopeFactory so the scoped AppDbContext is resolved correctly.
/// </summary>
public sealed class EmsAuditConsumer : BackgroundService
{
    private readonly RabbitMqSettings          _settings;
    private readonly ILogger<EmsAuditConsumer> _logger;
    private readonly IServiceScopeFactory      _scopeFactory;
    private IConnection? _connection;
    private IModel?      _channel;

    public EmsAuditConsumer(
        IOptions<RabbitMqSettings> settings,
        ILogger<EmsAuditConsumer> logger,
        IServiceScopeFactory scopeFactory)
    {
        _settings     = settings.Value;
        _logger       = logger;
        _scopeFactory = scopeFactory;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(Teardown);

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
                NetworkRecoveryInterval  = TimeSpan.FromSeconds(10),
                DispatchConsumersAsync   = false
            };

            _connection = factory.CreateConnection("EMS-AuditConsumer");
            _channel    = _connection.CreateModel();

            // Declare exchange (idempotent)
            _channel.ExchangeDeclare(
                exchange:   _settings.ExchangeName,
                type:       _settings.ExchangeType,
                durable:    true,
                autoDelete: false);

            // Declare and bind audit queue
            _channel.QueueDeclare(
                queue:      _settings.QueueName,
                durable:    true,
                exclusive:  false,
                autoDelete: false);

            _channel.QueueBind(
                queue:      _settings.QueueName,
                exchange:   _settings.ExchangeName,
                routingKey: $"{_settings.RoutingKeyPrefix}.#");

            // Fair dispatch — process one message at a time
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += OnMessageReceived;

            _channel.BasicConsume(
                queue:    _settings.QueueName,
                autoAck:  false,
                consumer: consumer);

            _logger.LogInformation(
                "EmsAuditConsumer started — Queue: {Queue}, BindingKey: {Key}",
                _settings.QueueName, $"{_settings.RoutingKeyPrefix}.#");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "EmsAuditConsumer could not connect to RabbitMQ — consumer will not run.");
        }

        return Task.CompletedTask;
    }

    private void OnMessageReceived(object? sender, BasicDeliverEventArgs ea)
    {
        var routingKey = ea.RoutingKey;
        var body       = Encoding.UTF8.GetString(ea.Body.ToArray());

        try
        {
            var evt = JsonSerializer.Deserialize<AuditEvent>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (evt is not null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.AuditLogs.Add(new AuditLog
                {
                    Action        = evt.Action,
                    Entity        = evt.Entity,
                    UserId        = evt.UserId,
                    Format        = evt.Format,
                    RecordCount   = evt.RecordCount,
                    EmployeeId    = evt.EmployeeId,
                    FileName      = evt.FileName,
                    FileSizeBytes = evt.FileSizeBytes,
                    Success       = evt.Success,
                    RoutingKey    = routingKey,
                    Timestamp     = evt.Timestamp,
                });
                db.SaveChanges();
            }

            _logger.LogInformation(
                "RabbitMQ event consumed + saved — RoutingKey: {RoutingKey}", routingKey);

            _channel?.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing RabbitMQ message — RoutingKey: {RoutingKey}. Message requeued.",
                routingKey);

            _channel?.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private void Teardown()
    {
        try { _channel?.Close();    } catch { /* ignore */ }
        try { _connection?.Close(); } catch { /* ignore */ }
        _channel?.Dispose();
        _connection?.Dispose();
    }

    public override void Dispose()
    {
        Teardown();
        base.Dispose();
    }
}
