using Microsoft.EntityFrameworkCore;
using UserManagementApi.Data;
using UserManagementApi.Models;

namespace UserManagementApi.Services;

public class IdempotencyService : IIdempotencyService
{
    private readonly AutoShopDbContext _db;
    public IdempotencyService(AutoShopDbContext db) => _db = db;

    public async Task<int?> GetResourceIdAsync(string key, string operation) {
        var rec = await _db.IdempotencyRecords
                           .AsNoTracking()
                           .FirstOrDefaultAsync(r => r.Key == key && r.Operation == operation);
        return rec?.ResourceId;
    }

    public async Task SaveResourceIdAsync(string key, string operation, int resourceId) {
        _db.IdempotencyRecords.Add(new IdempotencyRecord {
            Key = key,
            Operation = operation,
            ResourceId = resourceId,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
