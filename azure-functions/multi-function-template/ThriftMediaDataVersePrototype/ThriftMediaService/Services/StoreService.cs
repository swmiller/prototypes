using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using ThriftMediaService.Constants;
using ThriftMediaService.Models;

namespace ThriftMediaService.Services;

public class StoreService : IStoreService
{
    private readonly ILogger<StoreService> _logger;
    private readonly IDataverseConnectionService _dataverseConnectionService;
    private const string TableLogicalName = DataverseConstants.Tables.Store;

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
            [DataverseConstants.StoreColumns.Name] = store.Name,
            [DataverseConstants.StoreColumns.Address] = store.Address,
            [DataverseConstants.StoreColumns.City] = store.City,
            [DataverseConstants.StoreColumns.State] = store.State,
            [DataverseConstants.StoreColumns.ZipCode] = store.ZipCode,
            [DataverseConstants.StoreColumns.Phone] = store.Phone
        };

        var createdId = await service.CreateAsync(entity, CancellationToken.None);
        store.Id = createdId.ToString();

        // Retrieve the created entity to get the Dataverse-generated createdon value
        var createdEntity = await service.RetrieveAsync(
            entityName: TableLogicalName,
            id: createdId,
            columnSet: new ColumnSet(DataverseConstants.CommonColumns.CreatedOn),
            cancellationToken: CancellationToken.None
        );

        store.CreatedDate = createdEntity.GetAttributeValue<DateTime>(DataverseConstants.CommonColumns.CreatedOn);

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
                [DataverseConstants.StoreColumns.Name] = store.Name,
                [DataverseConstants.StoreColumns.Address] = store.Address,
                [DataverseConstants.StoreColumns.City] = store.City,
                [DataverseConstants.StoreColumns.State] = store.State,
                [DataverseConstants.StoreColumns.ZipCode] = store.ZipCode,
                [DataverseConstants.StoreColumns.Phone] = store.Phone
            };

            await service.UpdateAsync(entity, CancellationToken.None);
            store.Id = id;

            // Retrieve the updated entity to get the Dataverse-generated modifiedon value
            var updatedEntity = await service.RetrieveAsync(
                entityName: TableLogicalName,
                id: Guid.Parse(id),
                columnSet: new ColumnSet(DataverseConstants.CommonColumns.ModifiedOn),
                cancellationToken: CancellationToken.None
            );

            store.ModifiedDate = updatedEntity.GetAttributeValue<DateTime>(DataverseConstants.CommonColumns.ModifiedOn);

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
            Name = entity.GetAttributeValue<string>(DataverseConstants.StoreColumns.Name) ?? string.Empty,
            Address = entity.GetAttributeValue<string>(DataverseConstants.StoreColumns.Address) ?? string.Empty,
            City = entity.GetAttributeValue<string>(DataverseConstants.StoreColumns.City) ?? string.Empty,
            State = entity.GetAttributeValue<string>(DataverseConstants.StoreColumns.State) ?? string.Empty,
            ZipCode = entity.GetAttributeValue<string>(DataverseConstants.StoreColumns.ZipCode) ?? string.Empty,
            Phone = entity.GetAttributeValue<string>(DataverseConstants.StoreColumns.Phone) ?? string.Empty,
            CreatedDate = entity.GetAttributeValue<DateTime>(DataverseConstants.CommonColumns.CreatedOn),
            ModifiedDate = entity.Contains(DataverseConstants.CommonColumns.ModifiedOn)
                ? entity.GetAttributeValue<DateTime>(DataverseConstants.CommonColumns.ModifiedOn)
                : null
        };
    }

    private static ColumnSet GetColumnSetForStore()
    {
        return new ColumnSet(
            DataverseConstants.StoreColumns.Name,
            DataverseConstants.StoreColumns.Address,
            DataverseConstants.StoreColumns.City,
            DataverseConstants.StoreColumns.State,
            DataverseConstants.StoreColumns.ZipCode,
            DataverseConstants.StoreColumns.Phone,
            DataverseConstants.CommonColumns.CreatedOn,
            DataverseConstants.CommonColumns.ModifiedOn
        );
    }
}
