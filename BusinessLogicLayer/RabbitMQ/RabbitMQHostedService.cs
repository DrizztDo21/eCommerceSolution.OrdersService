using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace BusinessLogicLayer.RabbitMQ;

public class RabbitMQHostedService : IHostedService, IDisposable
{
    private readonly ILogger<RabbitMQHostedService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IEnumerable<IRabbitMQConsumer> _consumers;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMQHostedService(IConfiguration configuration, IEnumerable<IRabbitMQConsumer> consumers, ILogger<RabbitMQHostedService> logger)
    {
        _configuration = configuration;
        _consumers = consumers;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting RabbitMQ Hosted Service");

        var factory = new ConnectionFactory
        {
            HostName = _configuration["RABBITMQ_HOST"]!,
            Port = int.Parse(_configuration["RABBITMQ_PORT"]!),
            UserName = _configuration["RABBITMQ_USER"]!,
            Password = _configuration["RABBITMQ_PASSWORD"]!
        };

        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        foreach (var consumer in _consumers)
        {
            await consumer.StartConsumingAsync(_channel);
        }

    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
            await _channel.CloseAsync();

        if (_connection != null)
            await _connection.CloseAsync();
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }


}
