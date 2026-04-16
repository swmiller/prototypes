using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using ThriftMediaService.Models;

namespace ThriftMediaService.Services;

// TODO: Get rid of magic strings for column names and use constants or a mapping instead.

public class StoreService : IStoreService
{
    private readonly ILogger<StoreService> _logger;
    private readonly IDataverseConnectionService _dataverseConnectionService;
    private const string TableLogicalName = "cr1b3_store";

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

        var entity = new Entity(TableLogicalName)
        {
            ["cr1b3_storename"] = store.Name,
            ["cr1b3_address"] = store.Address,
            ["cr1b3_city"] = store.City,
            ["cr1b3_state"] = store.State,
            ["cr1b3_zipcode"] = store.ZipCode,
            ["cr1b3_phone"] = store.Phone
        };

        var createdId = await service.CreateAsync(entity, CancellationToken.None);
        store.Id = createdId.ToString();

        // Retrieve the created entity to get the Dataverse-generated createdon value
        var createdEntity = await service.RetrieveAsync(
            entityName: TableLogicalName,
            id: createdId,
            columnSet: new ColumnSet("createdon"),
            cancellationToken: CancellationToken.None
        );

        store.CreatedDate = createdEntity.GetAttributeValue<DateTime>("createdon");

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
                ["cr1b3_storename"] = store.Name,
                ["cr1b3_address"] = store.Address,
                ["cr1b3_city"] = store.City,
                ["cr1b3_state"] = store.State,
                ["cr1b3_zipcode"] = store.ZipCode,
                ["cr1b3_phone"] = store.Phone
            };

            await service.UpdateAsync(entity, CancellationToken.None);
            store.Id = id;

            // Retrieve the updated entity to get the Dataverse-generated modifiedon value
            var updatedEntity = await service.RetrieveAsync(
                entityName: TableLogicalName,
                id: Guid.Parse(id),
                columnSet: new ColumnSet("modifiedon"),
                cancellationToken: CancellationToken.None
            );

            store.ModifiedDate = updatedEntity.GetAttributeValue<DateTime>("modifiedon");

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
            Name = entity.GetAttributeValue<string>("cr1b3_storename") ?? string.Empty,
            Address = entity.GetAttributeValue<string>("cr1b3_address") ?? string.Empty,
            City = entity.GetAttributeValue<string>("cr1b3_city") ?? string.Empty,
            State = entity.GetAttributeValue<string>("cr1b3_state") ?? string.Empty,
            ZipCode = entity.GetAttributeValue<string>("cr1b3_zipcode") ?? string.Empty,
            Phone = entity.GetAttributeValue<string>("cr1b3_phone") ?? string.Empty,
            CreatedDate = entity.GetAttributeValue<DateTime>("createdon"),
            ModifiedDate = entity.Contains("modifiedon")
                ? entity.GetAttributeValue<DateTime>("modifiedon")
                : null
        };
    }

    private static ColumnSet GetColumnSetForStore()
    {
        return new ColumnSet(
            "cr1b3_storename",
            "cr1b3_address",
            "cr1b3_city",
            "cr1b3_state",
            "cr1b3_zipcode",
            "cr1b3_phone",
            "createdon",
            "modifiedon"
        );
    }
}
