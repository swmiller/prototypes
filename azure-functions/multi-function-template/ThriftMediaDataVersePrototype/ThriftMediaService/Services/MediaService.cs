using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using ThriftMediaService.Constants;
using ThriftMediaService.Models;

namespace ThriftMediaService.Services;

public class MediaService : IMediaService
{
    private readonly ILogger<MediaService> _logger;
    private readonly IDataverseConnectionService _dataverseConnectionService;
    private const string TableLogicalName = DataverseConstants.Tables.Media;
    private const string StoreTableLogicalName = DataverseConstants.Tables.Store;

    public MediaService(ILogger<MediaService> logger, IDataverseConnectionService dataverseConnectionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataverseConnectionService = dataverseConnectionService ?? throw new ArgumentNullException(nameof(dataverseConnectionService));
    }

    public async Task<IEnumerable<Media>> GetAllMediaAsync()
    {
        _logger.LogInformation("Getting all media");

        var service = _dataverseConnectionService.GetService();
        var query = new QueryExpression(TableLogicalName)
        {
            ColumnSet = GetColumnSetForMedia()
        };

        var results = await service.RetrieveMultipleAsync(query, CancellationToken.None);
        return results.Entities.Select(MapToMedia).ToList();
    }

    public async Task<IEnumerable<Media>> GetMediaByStoreIdAsync(string storeId)
    {
        _logger.LogInformation("Getting media for store ID: {StoreId}", storeId);

        var service = _dataverseConnectionService.GetService();
        var query = new QueryExpression(TableLogicalName)
        {
            ColumnSet = GetColumnSetForMedia(),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(
                        DataverseConstants.MediaColumns.StoreId,
                        ConditionOperator.Equal,
                        Guid.Parse(storeId)
                    )
                }
            }
        };

        var results = await service.RetrieveMultipleAsync(query, CancellationToken.None);
        return results.Entities.Select(MapToMedia).ToList();
    }

    public async Task<Media?> GetMediaByIdAsync(string id)
    {
        _logger.LogInformation("Getting media with ID: {MediaId}", id);

        var service = _dataverseConnectionService.GetService();
        try
        {
            var entity = await service.RetrieveAsync(
                entityName: TableLogicalName,
                id: Guid.Parse(id),
                columnSet: GetColumnSetForMedia(),
                cancellationToken: CancellationToken.None
            );

            return MapToMedia(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving media with ID: {MediaId}", id);
            return null;
        }
    }

    public async Task<Media> CreateMediaAsync(Media media)
    {
        _logger.LogInformation("Creating media: {MediaTitle}", media.Title);

        var service = _dataverseConnectionService.GetService();

        var entity = new Entity(TableLogicalName)
        {
            [DataverseConstants.MediaColumns.Title] = media.Title,
            [DataverseConstants.MediaColumns.Description] = media.Description,
            [DataverseConstants.MediaColumns.MediaType] = media.MediaType,
            [DataverseConstants.MediaColumns.Url] = media.Url,
            [DataverseConstants.MediaColumns.StoreId] = new EntityReference(StoreTableLogicalName, Guid.Parse(media.StoreId))
        };

        var createdId = await service.CreateAsync(entity, CancellationToken.None);
        media.Id = createdId.ToString();

        // Retrieve the created entity to get the Dataverse-generated createdon value
        var createdEntity = await service.RetrieveAsync(
            entityName: TableLogicalName,
            id: createdId,
            columnSet: new ColumnSet(DataverseConstants.CommonColumns.CreatedOn),
            cancellationToken: CancellationToken.None
        );

        media.CreatedDate = createdEntity.GetAttributeValue<DateTime>(DataverseConstants.CommonColumns.CreatedOn);

        return media;
    }

    public async Task<Media?> UpdateMediaAsync(string id, Media media)
    {
        _logger.LogInformation("Updating media with ID: {MediaId}", id);

        var service = _dataverseConnectionService.GetService();

        try
        {
            var entity = new Entity(TableLogicalName)
            {
                Id = Guid.Parse(id),
                [DataverseConstants.MediaColumns.Title] = media.Title,
                [DataverseConstants.MediaColumns.Description] = media.Description,
                [DataverseConstants.MediaColumns.MediaType] = media.MediaType,
                [DataverseConstants.MediaColumns.Url] = media.Url,
                [DataverseConstants.MediaColumns.StoreId] = new EntityReference(StoreTableLogicalName, Guid.Parse(media.StoreId))
            };

            await service.UpdateAsync(entity, CancellationToken.None);
            media.Id = id;

            // Retrieve the updated entity to get the Dataverse-generated modifiedon value
            var updatedEntity = await service.RetrieveAsync(
                entityName: TableLogicalName,
                id: Guid.Parse(id),
                columnSet: new ColumnSet(DataverseConstants.CommonColumns.ModifiedOn),
                cancellationToken: CancellationToken.None
            );

            media.ModifiedDate = updatedEntity.GetAttributeValue<DateTime>(DataverseConstants.CommonColumns.ModifiedOn);

            return media;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating media {MediaId}", id);
            return null;
        }
    }

    public async Task<bool> DeleteMediaAsync(string id)
    {
        _logger.LogInformation("Deleting media with ID: {MediaId}", id);

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
            _logger.LogError(ex, "Error deleting media {MediaId}", id);
            return false;
        }
    }

    private static Media MapToMedia(Entity entity)
    {
        return new Media
        {
            Id = entity.Id.ToString(),
            Title = entity.GetAttributeValue<string>(DataverseConstants.MediaColumns.Title) ?? string.Empty,
            Description = entity.GetAttributeValue<string>(DataverseConstants.MediaColumns.Description) ?? string.Empty,
            MediaType = entity.GetAttributeValue<string>(DataverseConstants.MediaColumns.MediaType) ?? string.Empty,
            Url = entity.GetAttributeValue<string>(DataverseConstants.MediaColumns.Url) ?? string.Empty,
            StoreId = entity.GetAttributeValue<EntityReference>(DataverseConstants.MediaColumns.StoreId)?.Id.ToString() ?? string.Empty,
            CreatedDate = entity.GetAttributeValue<DateTime>(DataverseConstants.CommonColumns.CreatedOn),
            ModifiedDate = entity.Contains(DataverseConstants.CommonColumns.ModifiedOn)
                ? entity.GetAttributeValue<DateTime>(DataverseConstants.CommonColumns.ModifiedOn)
                : null
        };
    }

    private static ColumnSet GetColumnSetForMedia()
    {
        return new ColumnSet(
            DataverseConstants.MediaColumns.Title,
            DataverseConstants.MediaColumns.Description,
            DataverseConstants.MediaColumns.MediaType,
            DataverseConstants.MediaColumns.Url,
            DataverseConstants.MediaColumns.StoreId,
            DataverseConstants.CommonColumns.CreatedOn,
            DataverseConstants.CommonColumns.ModifiedOn
        );
    }
}
