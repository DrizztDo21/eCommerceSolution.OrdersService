using AutoMapper;
using BusinessLogicLayer.DTO;
using BusinessLogicLayer.HTTPClients;
using BusinessLogicLayer.ServiceContracts;
using DataAccessLayer.Entities;
using DataAccessLayer.RepositoryContracts;
using FluentValidation;
using FluentValidation.Results;
using MongoDB.Driver;

namespace BusinessLogicLayer.Services;
public class OrdersService : IOrdersService
{
    private readonly IOrdersRepository _ordersRepository;
    private readonly IMapper _mapper;
    private readonly IValidator<OrderAddRequest> _orderAddRequestValidator;
    private readonly IValidator<OrderItemAddRequest> _orderItemAddRequestValidator;
    private readonly IValidator<OrderUpdateRequest> _orderUpdateRequestValidator;
    private readonly IValidator<OrderItemUpdateRequest> _orderItemUpdateRequestValidator;
    private readonly UsersMicroserviceClient _usersMicroserviceClient;
    private readonly ProductsMicroserviceClient _productsMicroserviceClient;

    public OrdersService(
        IOrdersRepository ordersRepository,
        IMapper mapper,
        IValidator<OrderAddRequest> orderAddRequestValidator,
        IValidator<OrderItemAddRequest> orderItemAddRequestValidator,
        IValidator<OrderUpdateRequest> orderUpdateRequestValidator,
        IValidator<OrderItemUpdateRequest> orderItemUpdateRequestValidator,
        UsersMicroserviceClient usersMicroserviceClient,
        ProductsMicroserviceClient productsMicroserviceClient)
    {
        _ordersRepository = ordersRepository;
        _mapper = mapper;
        _orderAddRequestValidator = orderAddRequestValidator;
        _orderItemAddRequestValidator = orderItemAddRequestValidator;
        _orderUpdateRequestValidator = orderUpdateRequestValidator;
        _orderItemUpdateRequestValidator = orderItemUpdateRequestValidator;
        _usersMicroserviceClient = usersMicroserviceClient;
        _productsMicroserviceClient = productsMicroserviceClient;
    }

    public async Task<OrderResponse?> AddOrder(OrderAddRequest orderAddRequest)
    {
        if (orderAddRequest == null)
        {
            throw new ArgumentNullException(nameof(orderAddRequest));
        }

        //Validate OrderAddRequest using FluentValidation

        ValidationResult orderAddRequestValidationResult =  await _orderAddRequestValidator.ValidateAsync(orderAddRequest);

        if (!orderAddRequestValidationResult.IsValid)
        {
            string errors = string.Join(", ", orderAddRequestValidationResult.Errors.Select(e => e.ErrorMessage));
            
            throw new ArgumentException(errors);
        }

        List<ProductDTO?> products = new List<ProductDTO?>();

        //Validate each OrderItemAddRequest using FluentValidation

        foreach (OrderItemAddRequest orderItem in orderAddRequest.OrderItems)
        {
            ValidationResult orderItemAddRequestValidationResult = await _orderItemAddRequestValidator.ValidateAsync(orderItem);
            if (!orderItemAddRequestValidationResult.IsValid)
            {
                string errors = string.Join(", ", orderItemAddRequestValidationResult.Errors.Select(e => e.ErrorMessage));
                
                throw new ArgumentException(errors);
            }

            //check if productID exist in products microservice
            ProductDTO? product = await _productsMicroserviceClient.GetProductByProductID(orderItem.ProductID);
            if (product == null)
            {
                throw new ArgumentException($"Invalid Product ID: {orderItem.ProductID}");
            }

            products.Add(product);
        }

        //check if UserID exist in users microservice

        UserDTO? user = await _usersMicroserviceClient.GetUserByUserID(orderAddRequest.UserID);

        if (user == null)
        {
            throw new ArgumentException($"Invalid User ID: {orderAddRequest.UserID}");
        }

        //Convert OrderAddRequest to Order
        Order orderInput = _mapper.Map<Order>(orderAddRequest);

        //Generate values
        foreach (OrderItem orderItem in orderInput.OrderItems)
        {
            orderItem.TotalPrice = orderItem.Quantity * orderItem.UnitPrice;
        }

        orderInput.TotalBill = orderInput.OrderItems.Sum(oi => oi.TotalPrice);

        //Add Order to the collection
        Order? addedOrder = await _ordersRepository.AddOrder(orderInput);

        if (addedOrder == null)
        {
            return null;
        }

        //Convert the added Order to OrderResponse
        OrderResponse adderOrderResponse = _mapper.Map<OrderResponse>(addedOrder);

        //Load Product Name and category in each OrderItem
        if (adderOrderResponse != null)
        {

            foreach (OrderItemResponse? orderItem in adderOrderResponse.OrderItems)
            {
                if (orderItem == null)
                {
                    continue;
                }
                ProductDTO? product = products.Where(temp => temp.ProductID == orderItem.ProductID).FirstOrDefault();

                if (product == null)
                {
                    continue;
                }
                _mapper.Map<ProductDTO, OrderItemResponse>(product, orderItem);
            }

            //Load userPersonName and Email from users microservice
            _mapper.Map<UserDTO, OrderResponse>(user, adderOrderResponse);
        }

        return adderOrderResponse;
    }

    public async Task<bool> DeleteOrder(Guid orderID)
    {
        FilterDefinition<Order> filter = Builders<Order>.Filter.Eq(o => o.OrderID, orderID);

        Order? existingOrder = await _ordersRepository.GetOrderByCondition(filter);

        if (existingOrder == null)
        {
            return false;
        }

        bool isDeleted = await _ordersRepository.DeleteOrder(orderID);

        return isDeleted;
    }

    public async Task<OrderResponse?> GetOrderByCondition(FilterDefinition<Order> filter)
    {
        Order? existingOrder = await _ordersRepository.GetOrderByCondition(filter);

        if (existingOrder == null)
        {
            return null;
        }

        OrderResponse orderResponse = _mapper.Map<OrderResponse>(existingOrder);

        //Load Product Name and category in each OrderItem
        if (orderResponse != null)
        {

            foreach (OrderItemResponse? orderItem in orderResponse.OrderItems)
            {
                if (orderItem == null)
                {
                    continue;
                }
                ProductDTO? product = await _productsMicroserviceClient.GetProductByProductID(orderItem.ProductID);

                if (product == null)
                {
                    continue;
                }
                _mapper.Map<ProductDTO, OrderItemResponse>(product, orderItem);
            }

            //Load userPersonName and Email from users microservice
            UserDTO? user = await _usersMicroserviceClient.GetUserByUserID(orderResponse.UserID);

            if (user != null)
            {
                _mapper.Map<UserDTO, OrderResponse>(user, orderResponse);
            }
        }

        return orderResponse;
    }

    public async Task<List<OrderResponse?>> GetOrders()
    {
        IEnumerable<Order> orders = await _ordersRepository.GetOrders();

        List<OrderResponse?> orderResponses = _mapper.Map<List<OrderResponse?>>(orders);

        //Load Product Name and category in each OrderItem
        foreach (OrderResponse? orderResponse in orderResponses)
        {
            if (orderResponse == null)
            {
                continue;
            }

            foreach (OrderItemResponse? orderItem in orderResponse.OrderItems)
            {
                if (orderItem == null)
                {
                    continue;
                }
                ProductDTO? product = await _productsMicroserviceClient.GetProductByProductID(orderItem.ProductID);

                if (product == null)
                {
                    continue;
                }
                _mapper.Map<ProductDTO, OrderItemResponse>(product, orderItem);
            }

            //Load userPersonName and Email from users microservice
            UserDTO? user = await _usersMicroserviceClient.GetUserByUserID(orderResponse.UserID);

            if(user != null)
            {
                _mapper.Map<UserDTO, OrderResponse>(user, orderResponse);
            }
        }     

        return orderResponses;
    }

    public async Task<List<OrderResponse?>> GetOrdersByCondition(FilterDefinition<Order> filter)
    {
        IEnumerable<Order?> orders = await _ordersRepository.GetOrdersByCondition(filter);

        List<OrderResponse?> orderResponses = _mapper.Map<List<OrderResponse?>>(orders);

        //Load Product Name and category in each OrderItem
        foreach (OrderResponse? orderResponse in orderResponses)
        {
            if (orderResponse == null)
            {
                continue;
            }

            foreach (OrderItemResponse? orderItem in orderResponse.OrderItems)
            {
                if (orderItem == null)
                {
                    continue;
                }
                ProductDTO? product = await _productsMicroserviceClient.GetProductByProductID(orderItem.ProductID);

                if (product == null)
                {
                    continue;
                }
                _mapper.Map<ProductDTO, OrderItemResponse>(product, orderItem);
            }

            //Load userPersonName and Email from users microservice
            UserDTO? user = await _usersMicroserviceClient.GetUserByUserID(orderResponse.UserID);

            if (user != null)
            {
                _mapper.Map<UserDTO, OrderResponse>(user, orderResponse);
            }
        }

        return orderResponses;
    }

    public async Task<OrderResponse?> UpdateOrder(OrderUpdateRequest orderUpdateRequest)
    {
        if(orderUpdateRequest == null)
        {
            throw new ArgumentNullException(nameof(orderUpdateRequest));
        }

        //Validate orderUpdateRequest using FluentValidation

        ValidationResult orderUpdateRequestValidationResult = await _orderUpdateRequestValidator.ValidateAsync(orderUpdateRequest);

        if (!orderUpdateRequestValidationResult.IsValid)
        {
            string errors = string.Join(", ", orderUpdateRequestValidationResult.Errors.Select(e => e.ErrorMessage));

            throw new ArgumentException(errors);
        }

        List<ProductDTO?> products = new List<ProductDTO?>();
        //Validate each OrderItemUpdateRequest using FluentValidation

        foreach (OrderItemUpdateRequest orderItem in orderUpdateRequest.OrderItems)
        {
            ValidationResult orderItemUpdateRequestValidationResult = await _orderItemUpdateRequestValidator.ValidateAsync(orderItem);
            if (!orderItemUpdateRequestValidationResult.IsValid)
            {
                string errors = string.Join(", ", orderItemUpdateRequestValidationResult.Errors.Select(e => e.ErrorMessage));

                throw new ArgumentException(errors);
            }

            //check if productID exist in products microservice
            ProductDTO? product = await _productsMicroserviceClient.GetProductByProductID(orderItem.ProductID);
            if (product == null)
            {
                throw new ArgumentException($"Invalid Product ID: {orderItem.ProductID}");
            }

            products.Add(product);
        }

        //check if UserID exist in users microservice

        UserDTO? user = await _usersMicroserviceClient.GetUserByUserID(orderUpdateRequest.UserID);

        if (user == null)
        {
            throw new ArgumentException("Invalid User ID");
        }

        //Convert OrderUpdateRequest to Order
        Order orderInput = _mapper.Map<Order>(orderUpdateRequest);

        //Generate values
        foreach (OrderItem orderItem in orderInput.OrderItems)
        {
            orderItem.TotalPrice = orderItem.Quantity * orderItem.UnitPrice;
        }

        orderInput.TotalBill = orderInput.OrderItems.Sum(oi => oi.TotalPrice);

        //Update order in the collection
        Order? updatedOrder = await _ordersRepository.UpdateOrder(orderInput);

        if (updatedOrder == null)
        {
            return null;
        }

        //Convert the added Order to OrderResponse
        OrderResponse updatedOrderResponse = _mapper.Map<OrderResponse>(updatedOrder);

        //Load Product Name and category in each OrderItem
        if (updatedOrderResponse != null)
        {

            foreach (OrderItemResponse? orderItem in updatedOrderResponse.OrderItems)
            {
                if (orderItem == null)
                {
                    continue;
                }
                ProductDTO? product = products.Where(temp => temp.ProductID == orderItem.ProductID).FirstOrDefault();

                if (product == null)
                {
                    continue;
                }
                _mapper.Map<ProductDTO, OrderItemResponse>(product, orderItem);
            }

            //Load userPersonName and Email from users microservice
            _mapper.Map<UserDTO, OrderResponse>(user, updatedOrderResponse);
        }

        return updatedOrderResponse;
    }
}
