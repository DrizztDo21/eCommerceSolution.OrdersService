using BusinessLogicLayer.DTO;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace BusinessLogicLayer.RabbitMQ;
public class RabbitMQProductNameUpdateConsumer : IRabbitMQConsumer
{
    private IChannel? _channel;
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RabbitMQProductNameUpdateConsumer> _logger;

    public RabbitMQProductNameUpdateConsumer(IConfiguration configuration, ILogger<RabbitMQProductNameUpdateConsumer> logger, IDistributedCache distributedCache)
    {
        _configuration = configuration;
        _logger = logger;
        _distributedCache = distributedCache;
    }

    public async Task StartConsumingAsync(IChannel channel)
    {

        _logger.LogInformation($"orders.product.update.name.queue");
        _channel = channel;

        string routingKey = "product.update.name";
        string queueName = "orders.product.update.name.queue";

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
                ProductNameUpdateMessage? productNameUpdateMessage = JsonSerializer.Deserialize<ProductNameUpdateMessage>(message);

                _logger.LogInformation("Received ProductNameUpdateMessage: {message}", message);

                if (productNameUpdateMessage != null)
                {
                    await HandleCacheProductNameUpdate(productNameUpdateMessage);
                }
            }
        };

        await _channel.BasicConsumeAsync(
        queue: queueName,
        autoAck: true,
        consumer: consumer);
    }

    private async Task HandleCacheProductNameUpdate(ProductNameUpdateMessage productNameUpdateMessage)
    {
        //If the product is already in cache, update the name in cache
        string cacheKey = $"Product:{productNameUpdateMessage.ProductID}";

        string? cachedProduct = await _distributedCache.GetStringAsync(cacheKey);

        if (cachedProduct != null)
        {
            ProductDTO? productFromCache = JsonSerializer.Deserialize<ProductDTO>(cachedProduct)! with { ProductName = productNameUpdateMessage.NewProductName };

            string productJson = JsonSerializer.Serialize(productFromCache);
            string productCacheKey = $"Product:{productFromCache.ProductID}";
            DistributedCacheEntryOptions options = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(150))
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(600));

            await _distributedCache.SetStringAsync(productCacheKey, productJson, options);

            _logger.LogInformation($"Updated Product in cache with key: {productCacheKey}");
        }
        else
        {
            _logger.LogInformation($"Product not found in cache with key: {cacheKey}");
        }
    }
}
