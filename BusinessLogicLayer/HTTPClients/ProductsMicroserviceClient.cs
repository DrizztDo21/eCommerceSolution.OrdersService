using BusinessLogicLayer.DTO;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Polly.Bulkhead;
using System.Net.Http.Json;
using System.Text.Json;

namespace BusinessLogicLayer.HTTPClients;
public class ProductsMicroserviceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductsMicroserviceClient> _logger;
    private readonly IDistributedCache _distributedCache;

    public ProductsMicroserviceClient(HttpClient httpClient, ILogger<ProductsMicroserviceClient> logger, IDistributedCache distributedCache)
    {
        _httpClient = httpClient;
        _logger = logger;
        _distributedCache = distributedCache;
    }

    public async Task<ProductDTO?> GetProductByProductID(Guid productID)
    {
        try
        {

            //Read from redis cache
            string cacheKey = $"Product:{productID}";

            string? cachedProduct = await _distributedCache.GetStringAsync(cacheKey);

            if(cachedProduct != null)
            {
                ProductDTO? productFromCache = JsonSerializer.Deserialize<ProductDTO>(cachedProduct);

                return productFromCache;
            }

            //If not in cache, call the microservice

            HttpResponseMessage response = await _httpClient.GetAsync($"/gateway/products/search/product-id/{productID}");

            //Handle non-success status codes
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    //Return dummy product without caching it
                    ProductDTO? dummyProduct = await response.Content.ReadFromJsonAsync<ProductDTO?>();

                    if (dummyProduct == null)
                    {
                        throw new NotImplementedException("Fallback policy not implemented");
                    }

                    return dummyProduct;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
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


            //Read and deserialize the response content
            ProductDTO? product = await response.Content.ReadFromJsonAsync<ProductDTO?>();

            if (product == null)
            {
                throw new ArgumentException("Invalid Product ID");
            }

            //Store in redis cache
            string productJson = JsonSerializer.Serialize(product);
            string productCacheKey = $"Product:{productID}";
            DistributedCacheEntryOptions options = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(150))
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(600));

            await _distributedCache.SetStringAsync(productCacheKey, productJson,options);

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
