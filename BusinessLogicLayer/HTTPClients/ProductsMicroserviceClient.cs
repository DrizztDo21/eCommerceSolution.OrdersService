using BusinessLogicLayer.DTO;
using Microsoft.Extensions.Logging;
using Polly.Bulkhead;
using System.Net.Http.Json;

namespace BusinessLogicLayer.HTTPClients;
public class ProductsMicroserviceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductsMicroserviceClient> _logger;
    public ProductsMicroserviceClient(HttpClient httpClient, ILogger<ProductsMicroserviceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    public async Task<ProductDTO?> GetProductByProductID(Guid productID)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/products/search/product-id/{productID}");

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    throw new HttpRequestException("Bad request", null, System.Net.HttpStatusCode.BadRequest);
                }
                else
                {
                    throw new HttpRequestException($"Https request failed with status code {response.StatusCode}");
                }
            }

            ProductDTO? product = await response.Content.ReadFromJsonAsync<ProductDTO?>();

            if (product == null)
            {
                throw new ArgumentException("Invalid Product ID");
            }

            return product;
        }
        catch (BulkheadRejectedException ex)
        {
            _logger.LogError(ex, "Bulkhead Rejection: Too many concurrent requests to Products Microservice.");

            return new ProductDTO
            {
                ProductID = Guid.Empty,
                ProductName = "Service Unavailable",
                Category = "Service Unavailable",
                UnitPrice = 0,
                QuantityInStock = 0
            };
        }
    }
}
