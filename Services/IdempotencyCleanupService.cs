using UserManagementApi.Data;

public class IdempotencyCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    public IdempotencyCleanupService(IServiceScopeFactory scopeFactory) =>
        _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AutoShopDbContext>();

            var cutoff = DateTime.UtcNow.AddDays(-7); // keep 7 days
            var oldRecords = db.IdempotencyRecords.Where(r => r.CreatedAt < cutoff);
            db.IdempotencyRecords.RemoveRange(oldRecords);
            await db.SaveChangesAsync(stoppingToken);
        }
    }
}
