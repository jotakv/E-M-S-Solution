namespace ServerLibrary.Helpers;

public class RabbitMqSettings
{
    public string HostName         { get; set; } = "localhost";
    public int    Port             { get; set; } = 5672;
    public string UserName         { get; set; } = "guest";
    public string Password         { get; set; } = "guest";
    public string VirtualHost      { get; set; } = "/";
    public string ExchangeName     { get; set; } = "ems.events";
    public string ExchangeType     { get; set; } = "topic";
    public string QueueName        { get; set; } = "ems.audit";
    public string RoutingKeyPrefix { get; set; } = "ems";
}
