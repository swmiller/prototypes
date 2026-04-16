using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using ThriftMediaService.Models;

namespace ThriftMediaService.Services;



// TODO: Modify column sets to use Dataverse schema names
// TODO: Don't create values for the ID column explicitly. Let Dataverse handle that.
public class StoreService : IStoreService
{
    private readonly ILogger<StoreService> _logger;
    private readonly IDataverseConnectionService _dataverseConnectionService;
    private const string TableLogicalName = "thriftmedia_store";

    public StoreService(ILogger<StoreService> logger, IDataverseConnectionService dataverseConnectionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataverseConnectionService = dataverseConnectionService ?? throw new ArgumentNullException(nameof(dataverseConnectionService));
    }

    public async Task<IEnumerable<Store>> GetAllStoresAsync()
    {
        _logger.LogInformation("Getting all stores");

        var service = _dataverseConnectionService.GetService();
        var query = new QueryExpression(TableLogicalName)
        {
            ColumnSet = GetColumnSetForStore()
        };

        // TODO: Fix cancellation token usage
        var results = await service.RetrieveMultipleAsync(query, CancellationToken.None);
        return results.Entities.Select(MapToStore).ToList();
    }

    public async Task<Store?> GetStoreByIdAsync(string id)
    {
        _logger.LogInformation("Getting store with ID: {StoreId}", id);

        var service = _dataverseConnectionService.GetService();
        try
        {
            var entity = await service.RetrieveAsync(
                entityName: TableLogicalName,
                id: Guid.Parse(id),
                columnSet: GetColumnSetForStore(),
                cancellationToken: CancellationToken.None
            );

            return MapToStore(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving store with ID: {StoreId}", id);
            return null;
        }
    }

    public async Task<Store> CreateStoreAsync(Store store)
    {
        _logger.LogInformation("Creating store: {StoreName}", store.Name);

        var service = _dataverseConnectionService.GetService();

        store.CreatedDate = DateTime.UtcNow;
        var entity = new Entity(TableLogicalName)
        {
            ["thriftmedia_name"] = store.Name,
            ["thriftmedia_address"] = store.Address,
            ["thriftmedia_city"] = store.City,
            ["thriftmedia_state"] = store.State,
            ["thriftmedia_zipcode"] = store.ZipCode,
            ["thriftmedia_phone"] = store.Phone,
            ["createdon"] = store.CreatedDate
        };

        var createdId = await service.CreateAsync(entity, CancellationToken.None);
        store.Id = createdId.ToString();

        return store;
    }

    public async Task<Store?> UpdateStoreAsync(string id, Store store)
    {
        _logger.LogInformation("Updating store with ID: {StoreId}", id);

        var service = _dataverseConnectionService.GetService();

        try
        {
            var entity = new Entity(TableLogicalName)
            {
                Id = Guid.Parse(id),
                ["thriftmedia_name"] = store.Name,
                ["thriftmedia_address"] = store.Address,
                ["thriftmedia_city"] = store.City,
                ["thriftmedia_state"] = store.State,
                ["thriftmedia_zipcode"] = store.ZipCode,
                ["thriftmedia_phone"] = store.Phone
            };

            await service.UpdateAsync(entity, CancellationToken.None);
            store.Id = id;
            store.ModifiedDate = DateTime.UtcNow;

            return store;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating store {StoreId}", id);
            return null;
        }
    }

    public async Task<bool> DeleteStoreAsync(string id)
    {
        _logger.LogInformation("Deleting store with ID: {StoreId}", id);

        var service = _dataverseConnectionService.GetService();
        try
        {
            await service.DeleteAsync(
                entityName: TableLogicalName,
                id: Guid.Parse(id),
                cancellationToken: CancellationToken.None
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting store {StoreId}", id);
            return false;
        }
    }

    private static Store MapToStore(Entity entity)
    {
        return new Store
        {
            Id = entity.Id.ToString(),
            Name = entity.GetAttributeValue<string>("thriftmedia_name") ?? string.Empty,
            Address = entity.GetAttributeValue<string>("thriftmedia_address") ?? string.Empty,
            City = entity.GetAttributeValue<string>("thriftmedia_city") ?? string.Empty,
            State = entity.GetAttributeValue<string>("thriftmedia_state") ?? string.Empty,
            ZipCode = entity.GetAttributeValue<string>("thriftmedia_zipcode") ?? string.Empty,
            Phone = entity.GetAttributeValue<string>("thriftmedia_phone") ?? string.Empty,
            CreatedDate = entity.GetAttributeValue<DateTime>("createdon"),
            ModifiedDate = entity.Contains("modifiedon")
                ? entity.GetAttributeValue<DateTime>("modifiedon")
                : null
        };
    }

private static ColumnSet GetColumnSetForStore()
    {
        return new ColumnSet(
            "thriftmedia_storeid",
            "thriftmedia_name",
            "thriftmedia_address",
            "thriftmedia_city",
            "thriftmedia_state",
            "thriftmedia_zipcode",
            "thriftmedia_phone",
            "createdon",
            "modifiedon"
        );
    }
}
