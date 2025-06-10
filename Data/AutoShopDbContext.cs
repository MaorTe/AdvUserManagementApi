using Microsoft.EntityFrameworkCore;
using UserManagementApi.Models;

namespace UserManagementApi.Data;

public class AutoShopDbContext : DbContext
{
    public AutoShopDbContext(DbContextOptions<AutoShopDbContext> options)
        : base(options) {
    }
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }
    public DbSet<User> Users { get; set; }

    public DbSet<Car> Cars { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        // Unique index on Email
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // 1→N ,Car ↔ User relationship
        modelBuilder.Entity<User>()
            .HasOne(u => u.Car)
            .WithMany(c => c.Users)
            .HasForeignKey(u => u.CarId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

