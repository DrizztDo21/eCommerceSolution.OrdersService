using DataAccessLayer.Entities;
using DataAccessLayer.RepositoryContracts;
using MongoDB.Driver;

namespace DataAccessLayer.Repositories;
public class OrdersRepository : IOrdersRepository
{
    private readonly IMongoCollection<Order> _orders;
    private readonly string collectionName = "orders";

    public OrdersRepository(IMongoDatabase mongoDatabase)
    {
        _orders = mongoDatabase.GetCollection<Order>(collectionName);
    }

    public async Task<Order?> AddOrder(Order order)
    {
        order.OrderID = Guid.NewGuid();

        order._id = order.OrderID;

        foreach (OrderItem item in order.OrderItems)
        {
            item._id = Guid.NewGuid();
        }

        await _orders.InsertOneAsync(order);

        return order;
    }

    public async Task<bool> DeleteOrder(Guid orderID)
    {
        FilterDefinition<Order> filter = Builders<Order>.Filter.Eq(temp => temp.OrderID, orderID);
        Order? existingOrder = (await _orders.FindAsync(filter)).FirstOrDefault();

        if (existingOrder != null) 
        {
            return false;
        }

        DeleteResult deleteResult = await _orders.DeleteOneAsync(filter);

        return deleteResult.DeletedCount > 0;
    }

    public async Task<Order?> GetOrderByCondition(FilterDefinition<Order> filter)
    {
        Order? order = (await _orders.FindAsync(filter)).FirstOrDefault();

        return order;
    }

    public async Task<IEnumerable<Order>> GetOrders()
    {
        List<Order> orders = (await _orders.FindAsync(Builders<Order>.Filter.Empty)).ToList();

        return orders;
    }

    public async Task<IEnumerable<Order?>> GetOrdersByCondition(FilterDefinition<Order> filter)
    {
        List<Order> orders = (await _orders.FindAsync(filter)).ToList();

        return orders;
    }

    public async Task<Order?> UpdateOrder(Order order)
    {
        FilterDefinition<Order> filter = Builders<Order>.Filter.Eq(temp => temp.OrderID, order.OrderID);
        Order? existingOrder = (await _orders.FindAsync(filter)).FirstOrDefault();

        if (existingOrder == null)
        {
            return null;
        }

        order._id = existingOrder._id;

        ReplaceOneResult replaceOneResult = await _orders.ReplaceOneAsync(filter, order);

        return order;
    }
}
