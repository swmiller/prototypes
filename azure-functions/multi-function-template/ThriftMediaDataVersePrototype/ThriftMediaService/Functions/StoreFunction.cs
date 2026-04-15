using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ThriftMediaService.Models;
using ThriftMediaService.Services;

namespace ThriftMediaService.Functions;

public class StoreFunction
{
    private readonly ILogger<StoreFunction> _logger;
    private readonly IStoreService _storeService;

    public StoreFunction(ILogger<StoreFunction> logger, IStoreService storeService)
    {
        _logger = logger;
        _storeService = storeService;
    }

    [Function("GetStores")]
    public async Task<IActionResult> GetStores(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "stores")] HttpRequest req)
    {
        _logger.LogInformation("Getting all stores");
        
        var stores = await _storeService.GetAllStoresAsync();
        return new OkObjectResult(stores);
    }

    [Function("GetStoreById")]
    public async Task<IActionResult> GetStoreById(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "stores/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Getting store with ID: {StoreId}", id);
        
        var store = await _storeService.GetStoreByIdAsync(id);
        
        if (store == null)
        {
            return new NotFoundObjectResult(new { message = $"Store with ID {id} not found" });
        }
        
        return new OkObjectResult(store);
    }

    [Function("CreateStore")]
    public async Task<IActionResult> CreateStore(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "stores")] HttpRequest req)
    {
        _logger.LogInformation("Creating new store");
        
        var store = await req.ReadFromJsonAsync<Store>();
        
        if (store == null)
        {
            return new BadRequestObjectResult(new { message = "Invalid store data" });
        }
        
        var createdStore = await _storeService.CreateStoreAsync(store);
        
        return new CreatedResult($"/api/stores/{createdStore.Id}", createdStore);
    }

    [Function("UpdateStore")]
    public async Task<IActionResult> UpdateStore(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "stores/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Updating store with ID: {StoreId}", id);
        
        var store = await req.ReadFromJsonAsync<Store>();
        
        if (store == null)
        {
            return new BadRequestObjectResult(new { message = "Invalid store data" });
        }
        
        var updatedStore = await _storeService.UpdateStoreAsync(id, store);
        
        if (updatedStore == null)
        {
            return new NotFoundObjectResult(new { message = $"Store with ID {id} not found" });
        }
        
        return new OkObjectResult(updatedStore);
    }

    [Function("DeleteStore")]
    public async Task<IActionResult> DeleteStore(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "stores/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Deleting store with ID: {StoreId}", id);
        
        var result = await _storeService.DeleteStoreAsync(id);
        
        if (!result)
        {
            return new NotFoundObjectResult(new { message = $"Store with ID {id} not found" });
        }
        
        return new NoContentResult();
    }
}
