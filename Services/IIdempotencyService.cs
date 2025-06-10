namespace UserManagementApi.Services;

public interface IIdempotencyService
{
    Task<int?> GetResourceIdAsync(string key, string operation);
    Task SaveResourceIdAsync(string key, string operation, int resourceId);
}
