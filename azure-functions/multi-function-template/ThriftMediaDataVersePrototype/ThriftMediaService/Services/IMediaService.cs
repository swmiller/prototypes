using ThriftMediaService.Models;

namespace ThriftMediaService.Services;

public interface IMediaService
{
    Task<IEnumerable<Media>> GetAllMediaAsync();
    Task<IEnumerable<Media>> GetMediaByStoreIdAsync(string storeId);
    Task<Media?> GetMediaByIdAsync(string id);
    Task<Media> CreateMediaAsync(Media media);
    Task<Media?> UpdateMediaAsync(string id, Media media);
    Task<bool> DeleteMediaAsync(string id);
}
