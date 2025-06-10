using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using UserManagementApi.Services;

namespace UserManagementApi.Endpoints
{
    public static class DataMigrationEndpoints
    {
        public static WebApplication MapDataMigrationEndpoints(this WebApplication app) {
            var group = app.MapGroup("/admin/migrate");

            // 1. Export test DB table to local CSV and upload via SFTP
            group.MapPost("/export/{table}/{localPath}", async (
                string table,
                string localPath,
                IDataMigrationService migrationService) => {
                    await migrationService.ExportAndTransferAsync(table, localPath);
                    return Results.Ok(new {
                        Message = $"Export and transfer of '{table}' to '{localPath}' completed successfully.",
                        Table = table,
                        LocalPath = localPath
                    });
                })
            .WithName("ExportAndTransfer")
            .WithTags("DataMigration");

            // 2. Download CSV via SFTP and import into production DB table
            group.MapPost("/import/{remotePath}/{localPath}/{table}", async (
                string remotePath,
                string localPath,
                string table,
                IDataMigrationService migrationService) => {
                    await migrationService.DownloadAndImportAsync(remotePath, localPath, table);
                    return Results.Ok(new {
                        Message = $"Download and import of '{remotePath}' into table '{table}' completed successfully.",
                        RemotePath = remotePath,
                        LocalPath = localPath,
                        Table = table
                    });
                })
            .WithName("DownloadAndImport")
            .WithTags("DataMigration");

            return app;
        }
    }
}
