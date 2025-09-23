using BusinessLogicLayer.DTO;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace BusinessLogicLayer.RabbitMQ;
public class RabbitMQProductDeleteConsumer : IRabbitMQConsumer
{
    private IChannel? _channel;
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RabbitMQProductDeleteConsumer> _logger;

    public RabbitMQProductDeleteConsumer(IConfiguration configuration, ILogger<RabbitMQProductDeleteConsumer> logger, IDistributedCache distributedCache)
    {
        _configuration = configuration;
        _logger = logger;
        _distributedCache = distributedCache;
    }

    public async Task StartConsumingAsync(IChannel channel)
    {

        _logger.LogInformation($"Start consuming orders.product.delete.queu");
        _channel = channel;

        string routingKey = "product.delete";
        string queueName = "orders.product.delete.queue";

        //Create exchange
        string exchangeName = _configuration["RABBITMQ_PRODUCTS_EXCHANGE"]!;

        await _channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Direct,
            durable: true);

        //Create queue
        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        //Bind queue to exchange
        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: routingKey);

        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (sender, args) =>
        {
            byte[] body = args.Body.ToArray();
            string message = Encoding.UTF8.GetString(body);

            if (message != null)
            {
                ProductDeleteMessage? productDeleteMessage = JsonSerializer.Deserialize<ProductDeleteMessage>(message);

                if (productDeleteMessage != null)
                {
                    _logger.LogInformation("Received ProductDeleteMessage: {message}", message);

                    await HandleCacheProductDelete(productDeleteMessage);
                }


            }
        };

        await _channel.BasicConsumeAsync(
        queue: queueName,
        autoAck: true,
        consumer: consumer);
    }

    private async Task HandleCacheProductDelete(ProductDeleteMessage productDeleteMessage)
    {
        //If the product is already in cache, delete the product from cache
        string cacheKey = $"Product:{productDeleteMessage.ProductID}";
        string? cachedProduct = await _distributedCache.GetStringAsync(cacheKey);
        if (cachedProduct != null)
        {
            await _distributedCache.RemoveAsync(cacheKey);
            _logger.LogInformation($"Removed Product from cache with key: {cacheKey}");
        }
        else
        {
            _logger.LogInformation($"Product with key: {cacheKey} not found in cache");
        }
    }
}
