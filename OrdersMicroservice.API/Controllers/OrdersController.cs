using BusinessLogicLayer.DTO;
using BusinessLogicLayer.ServiceContracts;
using BusinessLogicLayer.Validators;
using DataAccessLayer.Entities;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OrdersMicroservice.API.Controllers; 

[Route("api/[controller]")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly IOrdersService _orderService;
    private readonly IValidator<OrderAddRequest> _orderAddRequestValidator;
    private readonly IValidator<OrderUpdateRequest> _orderUpdateRequestValidator;
    private readonly IValidator<OrderItemAddRequest> _orderItemAddRequestValidator;
    private readonly IValidator<OrderItemUpdateRequest> _orderItemUpdateRequestValidator;

    public OrdersController(
        IOrdersService orderService,
        IValidator<OrderUpdateRequest> orderUpdateRequestValidator,
        IValidator<OrderAddRequest> orderAddRequestValidator,
        IValidator<OrderItemUpdateRequest> orderItemUpdateRequestValidator,
        IValidator<OrderItemAddRequest> orderItemAddRequestValidator)
    {
        _orderService = orderService;
        _orderUpdateRequestValidator = orderUpdateRequestValidator;
        _orderAddRequestValidator = orderAddRequestValidator;
        _orderItemUpdateRequestValidator = orderItemUpdateRequestValidator;
        _orderItemAddRequestValidator = orderItemAddRequestValidator;
    }

    //GET: /api/Orders
    [HttpGet]
    public async Task<IEnumerable<OrderResponse?>> Get()
    {
        List<OrderResponse?> orders = await _orderService.GetOrders();

        return orders;
    }

    //GET: /api/Orders/search/orderid/{orderID}
    [HttpGet("search/orderid/{orderID}")]
    public async Task<OrderResponse?> GetOrderByOrderID(Guid orderID)
    {
        FilterDefinition<Order> filter = Builders<Order>.Filter.Eq(order => order._id, orderID);

        OrderResponse? order = await _orderService.GetOrderByCondition(filter);

        return order;
    }

    //GET: /api/Orders/search/productid/{productID}
    [HttpGet("search/productid/{productID}")]
    public async Task<List<OrderResponse?>> GetOrderByProductID(Guid productID)
    {
        FilterDefinition<Order> filter = Builders<Order>.Filter.ElemMatch(temp => temp.OrderItems, 
            Builders<OrderItem>.Filter.Eq(tempProduct => tempProduct.ProductID, productID));

        List<OrderResponse?> orders = await _orderService.GetOrdersByCondition(filter);

        return orders;
    }

    //GET: /api/Orders/search/orderDate/{orderDate}
    [HttpGet("search/orderDate/{orderDate}")]
    public async Task<List<OrderResponse?>> GetOrderByOrderDate(DateTime orderDate)
    {
        FilterDefinition<Order> filter = Builders<Order>.Filter.Eq(temp => temp.OrderDate.ToString("yyy-MM-dd"), orderDate.ToString("yyy-MM-dd"));

        List<OrderResponse?> orders = await _orderService.GetOrdersByCondition(filter);

        return orders;
    }

    //GET: /api/Orders/search/userid/{userID}
    [HttpGet("search/userid/{userID}")]
    public async Task<List<OrderResponse?>> GetOrderByUserID(Guid userID)
    {
        FilterDefinition<Order> filter = Builders<Order>.Filter.Eq(temp => temp.UserID, userID);

        List<OrderResponse?> orders = await _orderService.GetOrdersByCondition(filter);

        return orders;
    }

    //POST: /api/orders
    [HttpPost]
    public async Task<ActionResult<OrderResponse?>> Post(OrderAddRequest orderAddRequest)
    {
        // Check if the orderAddRequest is null
        if (orderAddRequest is null)
        {
            return BadRequest("Invalid order data");
        }

        //Validate orderAddRequest using fluent validation
        ValidationResult validationResult = _orderAddRequestValidator.Validate(orderAddRequest);

        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        //Validate each OrderItemAddRequest using FluentValidation

        foreach (OrderItemAddRequest orderItem in orderAddRequest.OrderItems)
        {
            ValidationResult orderItemAddRequestValidationResult = await _orderItemAddRequestValidator.ValidateAsync(orderItem);
            if (!orderItemAddRequestValidationResult.IsValid)
            {
                string errors = string.Join(", ", orderItemAddRequestValidationResult.Errors.Select(e => e.ErrorMessage));

                return BadRequest(errors);
            }
        }


        OrderResponse? order = await _orderService.AddOrder(orderAddRequest);
        if (order is null)
        {
            return BadRequest();
        }
        return Created($"/api/Orders/search/orderid/{order.OrderID}", order);
    }

    //PUT: /api/Orders/{orderID}
    [HttpPut("{orderID}")]
    public async Task<ActionResult<OrderResponse?>> Put(Guid orderID, OrderUpdateRequest orderUpdateRequest)
    {
        // Check if the orderUpdateRequest is null
        if (orderUpdateRequest is null)
        {
            return BadRequest("Invalid order data");
        }

        // Check if the orderID in the route matches the orderID in the body
        if (orderID != orderUpdateRequest.OrderID)
        {
            return BadRequest("Order ID in the URL doesn't match with the order ID in the Request body");
        }

        //Validate orderUpdateRequest using fluent validation
        ValidationResult validationResult = _orderUpdateRequestValidator.Validate(orderUpdateRequest);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }
        //Validate each OrderItemUpdateRequest using FluentValidation
        foreach (OrderItemUpdateRequest orderItem in orderUpdateRequest.OrderItems)
        {
            ValidationResult orderItemUpdateRequestValidationResult = await _orderItemUpdateRequestValidator.ValidateAsync(orderItem);
            if (!orderItemUpdateRequestValidationResult.IsValid)
            {
                string errors = string.Join(", ", orderItemUpdateRequestValidationResult.Errors.Select(e => e.ErrorMessage));
                return BadRequest(errors);
            }
        }

        OrderResponse? order = await _orderService.UpdateOrder(orderUpdateRequest);
        if (order is null)
        {
            return NotFound();
        }
        return Ok(order);
    }

    //DELETE: /api/orders/{orderID}
    [HttpDelete("{orderID}")]
    public async Task<ActionResult> Delete(Guid orderID)
    {
        if (orderID == Guid.Empty)
        {
            return BadRequest("OrderID cannot be empty");
        }

        bool isDeleted = await _orderService.DeleteOrder(orderID);

        if (!isDeleted)
        {
            return NoContent();
        }
        return Ok(isDeleted);
    }
}
