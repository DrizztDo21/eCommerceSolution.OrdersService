using BusinessLogicLayer.DTO;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Bulkhead;
using Polly.Fallback;
using Polly.Wrap;

namespace BusinessLogicLayer.Policies;
public class ProductsMicroservicePolicies : IProductsMicroservicePolicies
{
    private readonly ILogger<ProductsMicroservicePolicies> _logger;
    public ProductsMicroservicePolicies(ILogger<ProductsMicroservicePolicies> logger)
    {
        _logger = logger;
    }

    public IAsyncPolicy<HttpResponseMessage> GetBulkHeadIsolationPolicy()
    {
        AsyncBulkheadPolicy<HttpResponseMessage> policy = Policy.BulkheadAsync<HttpResponseMessage>(
            maxParallelization: 10, //Number of concurrent requests 
            maxQueuingActions: 40, //Number of requests that can be queued when the maxParallelization is reached
            onBulkheadRejectedAsync: (context) =>
            {
                _logger.LogWarning("Bulkhead Rejection: Too many concurrent requests to Products Microservice. Please try again later.");

                throw new BulkheadRejectedException("Bulkhead queue is full.");
            }
            );

        return policy;
    }

    public IAsyncPolicy<HttpResponseMessage> GetFallBackPolicy()
    {
        AsyncFallbackPolicy<HttpResponseMessage> policy = Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
              .FallbackAsync(async (context) =>
              {
                  _logger.LogInformation("Products Microservice is down. Falling back to default response.");

                  ProductDTO product = new ProductDTO
                  {
                      ProductID = Guid.Empty,
                      ProductName = "Temporarily Unavailable",
                      Category = "Temporarily Unavailable",
                      UnitPrice = 0,
                      QuantityInStock = 0
                  };

                  HttpResponseMessage response = new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
                  {
                      Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(product), System.Text.Encoding.UTF8, "application/json")
                  };

                  return response;

              });

        return policy;
    }

    public IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy()
    {
        var fallBackPolicy = GetFallBackPolicy();
        var bulkHeadIsolationPolicy = GetBulkHeadIsolationPolicy();

        AsyncPolicyWrap<HttpResponseMessage> combinedPolicy = Policy.WrapAsync(fallBackPolicy, bulkHeadIsolationPolicy);

        return combinedPolicy;
    }
}
