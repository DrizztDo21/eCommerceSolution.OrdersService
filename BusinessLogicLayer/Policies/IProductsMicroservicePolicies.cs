using Polly;

namespace BusinessLogicLayer.Policies;
public interface IProductsMicroservicePolicies
{
    IAsyncPolicy<HttpResponseMessage> GetFallBackPolicy();
    IAsyncPolicy<HttpResponseMessage> GetBulkHeadIsolationPolicy();
    IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy();

}
