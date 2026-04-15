using ThriftMediaService.Models;

namespace ThriftMediaService.Services;

public interface IStoreService
{
    Task<IEnumerable<Store>> GetAllStoresAsync();
    Task<Store?> GetStoreByIdAsync(string id);
    Task<Store> CreateStoreAsync(Store store);
    Task<Store?> UpdateStoreAsync(string id, Store store);
    Task<bool> DeleteStoreAsync(string id);
}
