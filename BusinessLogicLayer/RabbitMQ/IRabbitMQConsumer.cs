using RabbitMQ.Client;

namespace BusinessLogicLayer.RabbitMQ
{
    public interface IRabbitMQConsumer
    {
        Task StartConsumingAsync(IChannel channel);
    }
}