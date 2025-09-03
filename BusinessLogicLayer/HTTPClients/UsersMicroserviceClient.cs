using BusinessLogicLayer.DTO;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Net.Http.Json;

namespace BusinessLogicLayer.HTTPClients;
public class UsersMicroserviceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UsersMicroserviceClient> _logger;

    public UsersMicroserviceClient(HttpClient httpClient, ILogger<UsersMicroserviceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    public async Task<UserDTO?> GetUserByUserID(Guid userId)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/users/{userId}");

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

            UserDTO? user = await response.Content.ReadFromJsonAsync<UserDTO?>();

            if (user == null)
            {
                throw new ArgumentException("Invalid User ID");
            }

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
