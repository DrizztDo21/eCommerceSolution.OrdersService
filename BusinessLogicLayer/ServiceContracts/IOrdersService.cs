using BusinessLogicLayer.DTO;
using DataAccessLayer.Entities;
using MongoDB.Driver;

namespace BusinessLogicLayer.ServiceContracts;
public interface IOrdersService
{
    /// <summary>
    /// Retrieves the list of orders from the orders repository
    /// </summary>
    /// <returns>Returns a list of orderResponse objects</returns>
    Task<List<OrderResponse?>> GetOrders();
    
    /// <summary>
    /// Returns list of orders matching with given condition
    /// </summary>
    /// <param name="filter">Expression that represents condition to check</param>
    /// <returns> Resturns matching orders as a list of OrderResponse objects</returns>
    Task<List<OrderResponse?>> GetOrdersByCondition(FilterDefinition<Order> filter);

    /// <summary>
    /// Returns a single order that matches with given condition
    /// </summary>
    /// <param name="filter">Expression that represents condition to check</param>
    /// <returns> Resturns matching order as OrderResponse object or null if not found</returns>
    Task<OrderResponse?> GetOrderByCondition(FilterDefinition<Order> filter);

    /// <summary>
    /// Add order into the collection using orders repository
    /// </summary>
    /// <param name="orderAddRequest">Order to insert</param>
    /// <returns>Returns orderResponse object containing order details after inserting or null if insertion was unseccessful</returns>
    Task<OrderResponse?> AddOrder(OrderAddRequest orderAddRequest);

    /// <summary>
    /// Updates the existing order based on the orderID
    /// </summary>
    /// <param name="orderUpdateRequest">Order data to update</param>
    /// <returns>Returns orderResponse objetc after successful update or null otherwhise</returns>
    Task<OrderResponse?> UpdateOrder(OrderUpdateRequest orderUpdateRequest);

    /// <summary>
    /// Deletes an existing order based on given 
    /// </summary>
    /// <param name="orderID">OrderID to search and delete</param>
    /// <returns>Returns true if the deletion is successful false otherwhise</returns>
    Task<bool> DeleteOrder(Guid orderID);

}
