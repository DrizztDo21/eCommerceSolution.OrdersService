using BusinessLogicLayer.DTO;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Net.Http.Json;
using System.Text.Json;

namespace BusinessLogicLayer.HTTPClients;
public class UsersMicroserviceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UsersMicroserviceClient> _logger;
    private readonly IDistributedCache _distributedCache;

    public UsersMicroserviceClient(HttpClient httpClient, ILogger<UsersMicroserviceClient> logger, IDistributedCache distributedCache)
    {
        _httpClient = httpClient;
        _logger = logger;
        _distributedCache = distributedCache;
    }
    public async Task<UserDTO?> GetUserByUserID(Guid userId)
    {
        try
        {
            //Read from redis cache
            string cacheKey = $"User:{userId}";

            string? cachedUser = await _distributedCache.GetStringAsync(cacheKey);

            if (cachedUser != null)
            {
                UserDTO? userFromCache = JsonSerializer.Deserialize<UserDTO>(cachedUser);

                return userFromCache;
            }

            //If not in cache, call the microservice
            HttpResponseMessage response = await _httpClient.GetAsync($"/gateway/users/{userId}");

            //Handle non-success status codes
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    //Return dummy product without caching it
                    UserDTO? dummyUser = await response.Content.ReadFromJsonAsync<UserDTO?>();

                    if (dummyUser == null)
                    {
                        throw new NotImplementedException("Fallback policy not implemented");
                    }

                    return dummyUser;
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
                    //throw new HttpRequestException($"Https request failed with status code {response.StatusCode}");

                    //If the user microservice is down, return a temporary user object with real userID for proceed with order creation
                    //and check later how to complete the order when the user microservice is back up
                    return new UserDTO
                    {
                        UserID = userId,
                        PersonName = "Temporaly Unavailable",
                        Email = "Temporaly Unavailable",
                        Gender = "Temporaly Unavailable",
                    };
                }
            }

            //Read and deserialize the response content
            UserDTO? user = await response.Content.ReadFromJsonAsync<UserDTO?>();

            if (user == null)
            {
                throw new ArgumentException("Invalid User ID");
            }

            //Store in redis cache
            string userJson = JsonSerializer.Serialize(user);
            string userCacheKey = $"User:{userId}";
            DistributedCacheEntryOptions options = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(150))
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(600));

            await _distributedCache.SetStringAsync(userCacheKey, userJson, options);

            return user;
        }
        catch (BrokenCircuitException ex)
        {

            _logger.LogError(ex, "Circuit breaker is open. The Users microservice is temporarily unavailable.");

            return new UserDTO
            {
                UserID = userId,
                PersonName = "Temporaly Unavailable",
                Email = "Temporaly Unavailable",
                Gender = "Temporaly Unavailable",
            };
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "The request to the Users microservice timed out.");

            return new UserDTO
            {
                UserID = userId,
                PersonName = "Temporaly Unavailable",
                Email = "Temporaly Unavailable",
                Gender = "Temporaly Unavailable",
            };

        }
    }
}
