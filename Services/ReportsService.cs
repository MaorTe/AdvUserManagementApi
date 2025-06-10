// Services/ReportsService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserManagementApi.Data;
using UserManagementApi.Models;

namespace UserManagementApi.Services;

public class ReportsService : IReportsService
{
    private readonly AutoShopDbContext _db;
    private readonly IDataMigrationService _migration;
    private readonly IFileTransferService _files;
    private readonly ILogger<ReportsService> _logger;

    public ReportsService(
        AutoShopDbContext db,
        IDataMigrationService migration,
        IFileTransferService files,
        ILogger<ReportsService> logger) {
        _db = db;
        _migration = migration;
        _files = files;
        _logger = logger;
    }

    /// <summary>
    /// “SELECT Name FROM Users 
    ///  WHERE MONTH(CreatedAt) = MONTH(MAX(CreatedAt)) 
    ///  GROUP BY Name”
    /// </summary>
    public async Task<IEnumerable<string>> GetLatestMonthNamesAsync() {
        // 1) find the most recent CreatedAt across all users
        var lastDate = await _db.Users
                                .MaxAsync(u => u.CreatedAt);

        var month = lastDate.Month;

        // 2) return distinct names whose CreatedAt falls in that month
        return await _db.Users
                        .Where(u => u.CreatedAt.Month == month)
                        .GroupBy(u => u.Name)
                        .Select(g => g.Key)
                        .ToListAsync();
    }

    /// <summary>
    /// “SELECT Name FROM Users 
    ///  GROUP BY Name 
    ///  HAVING COUNT(*) > 1”
    /// </summary>
    public async Task<IEnumerable<string>> GetDuplicateNamesAsync() {
        return await _db.Users
                        .GroupBy(u => u.Name)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToListAsync();
    }

    /// <summary>
    /// “SELECT COUNT(u.Id) 
    ///  FROM Users AS u 
    ///  INNER JOIN Cars AS c ON u.CarId = c.Id”
    /// </summary>
    public async Task<int> CountUsersWithCarsAsync() {
        // Inner join: users that have a matching car
        return await _db.Users
                        .Join(_db.Cars,
                              u => u.CarId,
                              c => c.Id,
                              (u, c) => u)
                        .CountAsync();
    }

    /// <summary>
    /// “SELECT COUNT(c.Id) 
    ///  FROM Users AS u 
    ///  RIGHT OUTER JOIN Cars AS c ON u.CarId = c.Id”
    /// EF Core doesn’t support RIGHT JOIN directly, so invert to LEFT JOIN:
    /// </summary>
    public async Task<int> CountCarsWithoutUsersAsync() {
        // Left join from Cars to Users, then count cars that have no users
        return await _db.Cars
                        .GroupJoin(_db.Users,
                                   c => c.Id,
                                   u => u.CarId,
                                   (c, us) => new { Car = c, Users = us })
                        .SelectMany(
                            grp => grp.Users.DefaultIfEmpty(),
                            (grp, user) => new { grp.Car, User = user })
                        .CountAsync(x => x.User == null);
    }

    public async Task<IEnumerable<string>> MigrateAndGetDuplicateNamesAsync(
        string tableName,
        string localPath,
        string remotePath) {
        // 1. export test→CSV then upload to prod server
        await _migration.ExportAndTransferAsync(tableName, localPath);

        // 2. download from prod server→CSV then bulk-import into prod DB
        await _migration.DownloadAndImportAsync(remotePath, localPath, tableName);

        // 3. now run the duplicate-names query via EF on the (imported) prod table
        var duplicates = await _db.Set<User>()           // or _db.Users if tableName == "Users"
            .AsNoTracking()
            .GroupBy(u => u.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync();

        _logger.LogInformation("Ran migrate+duplicate-names on {Table}", tableName);
        return duplicates;
    }

    public async Task<IEnumerable<string>> GetDuplicateNamesFromCsvAsync(
        string remotePath,
        string localPath) {
        // 1. download CSV via SFTP
        await _files.DownloadAsync(remotePath, localPath);

        // 2. parse CSV and compute duplicates
        var counts = new Dictionary<string, int>();
        using var reader = new StreamReader(localPath);
        var header = (await reader.ReadLineAsync()).Split(',');
        // find the index of the Name column (quotes trimmed)
        var nameIndex = Array.FindIndex(header, c => c.Trim('"') == "Name");
        if (nameIndex < 0)
            throw new InvalidOperationException("CSV does not contain a Name column");

        string? line;
        while ((line = await reader.ReadLineAsync()) != null) {
            var fields = line.Split(',');
            var name = fields[nameIndex].Trim('"');
            counts[name] = counts.GetValueOrDefault(name, 0) + 1;
        }

        var duplicates = counts
            .Where(kv => kv.Value > 1)
            .Select(kv => kv.Key)
            .ToList();

        _logger.LogInformation("Ran CSV-duplicate-names on {File}", remotePath);
        return duplicates;
    }
}
