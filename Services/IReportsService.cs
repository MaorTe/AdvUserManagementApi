namespace UserManagementApi.Services;

public interface IReportsService
{
    Task<IEnumerable<string>> GetLatestMonthNamesAsync();
    Task<IEnumerable<string>> GetDuplicateNamesAsync();
    Task<int> CountUsersWithCarsAsync();
    Task<int> CountCarsWithoutUsersAsync();

    // 5th report: run full export→SFTP→import flow, then query via EF
    Task<IEnumerable<string>> MigrateAndGetDuplicateNamesAsync(
        string tableName,
        string localPath,
        string remotePath);

    // 6th report: download a CSV via SFTP and query duplicates from the file
    Task<IEnumerable<string>> GetDuplicateNamesFromCsvAsync(
        string remotePath,
        string localPath);
}
