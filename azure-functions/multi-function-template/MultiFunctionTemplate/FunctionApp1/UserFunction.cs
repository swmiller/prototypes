using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MultiFunctionTemplate;

public class UserFunction
{
    private const string RoutePrefix = "users";
    private readonly ILogger<UserFunction> _logger;

    public UserFunction(ILogger<UserFunction> logger)
    {
        _logger = logger;
    }

    [Function("GetUser")]
    public IActionResult GetUser([HttpTrigger(AuthorizationLevel.Function, "get", Route = $"{RoutePrefix}/{{id}}")] HttpRequest req, string id)
    {
        _logger.LogInformation($"Getting user with ID: {id}");
        return new OkObjectResult($"User ID: {id}");
    }

    [Function("GetAllUsers")]
    public IActionResult GetAllUsers([HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix)] HttpRequest req)
    {
        _logger.LogInformation("Getting all users");
        return new OkObjectResult(new[] { "User1", "User2", "User3" });
    }

    [Function("CreateUser")]
    public IActionResult CreateUser([HttpTrigger(AuthorizationLevel.Function, "post", Route = RoutePrefix)] HttpRequest req)
    {
        _logger.LogInformation("Creating a new user");
        return new OkObjectResult("User created successfully");
    }

    [Function("UpdateUser")]
    public IActionResult UpdateUser([HttpTrigger(AuthorizationLevel.Function, "put", Route = $"{RoutePrefix}/{{id}}")] HttpRequest req, string id)
    {
        _logger.LogInformation($"Updating user with ID: {id}");
        return new OkObjectResult($"User {id} updated successfully");
    }

    [Function("DeleteUser")]
    public IActionResult DeleteUser([HttpTrigger(AuthorizationLevel.Function, "delete", Route = $"{RoutePrefix}/{{id}}")] HttpRequest req, string id)
    {
        _logger.LogInformation($"Deleting user with ID: {id}");
        return new OkObjectResult($"User {id} deleted successfully");
    }
}