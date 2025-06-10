using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UserManagementApi.Services
{
    public interface IDataMigrationService
    {
        Task ExportAndTransferAsync(string tableName, string localPath);
        Task DownloadAndImportAsync(string remotePath, string localPath, string destinationTable);
    }

    public class DataMigrationService : IDataMigrationService
    {
        private readonly string _testConnectionString;
        private readonly string _prodConnectionString;
        private readonly IFileTransferService _fileTransferService;
        private readonly ILogger<DataMigrationService> _logger;
        private readonly string _remoteDirectory;

        public DataMigrationService(
            IConfiguration config,
            IFileTransferService fileTransferService,
            ILogger<DataMigrationService> logger) {
            _testConnectionString = config.GetConnectionString("TestDb");
            _prodConnectionString = config.GetConnectionString("ProdTestDb");
            _fileTransferService = fileTransferService;
            _logger = logger;
            _remoteDirectory = config["Sftp:RemoteDirectory"] ?? "/";
        }

        public async Task ExportAndTransferAsync(string tableName, string localPath) {
            using var conn = new SqlConnection(_testConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM [{tableName}]";

            using var reader = await cmd.ExecuteReaderAsync();
            using var writer = new StreamWriter(localPath, false, Encoding.UTF8);

            var columnCount = reader.FieldCount;
            for (int i = 0; i < columnCount; i++) {
                if (i > 0) writer.Write(',');
                writer.Write(reader.GetName(i));
            }
            writer.WriteLine();

            while (await reader.ReadAsync()) {
                for (int i = 0; i < columnCount; i++) {
                    if (i > 0) writer.Write(',');
                    var value = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i).ToString();
                    value = value.Replace("\"", "\"\"");
                    writer.Write('"'); writer.Write(value); writer.Write('"');
                }
                writer.WriteLine();
            }

            _logger.LogInformation("Exported table {Table} to CSV at {Path}", tableName, localPath);

            var remotePath = Path.Combine(_remoteDirectory, Path.GetFileName(localPath));
            await _fileTransferService.UploadAsync(localPath, remotePath);
            _logger.LogInformation("Uploaded CSV to remote {RemotePath}", remotePath);
        }

        public async Task DownloadAndImportAsync(string remotePath, string localPath, string destinationTable) {
            var sftpPath = Path.Combine(_remoteDirectory, remotePath);
            await _fileTransferService.DownloadAsync(sftpPath, localPath);
            _logger.LogInformation("Downloaded remote CSV from {RemotePath} to {LocalPath}", sftpPath, localPath);

            var table = new DataTable();
            using (var reader = new StreamReader(localPath, Encoding.UTF8)) {
                var header = (await reader.ReadLineAsync()).Split(',');
                foreach (var col in header)
                    table.Columns.Add(col.Trim('"'));

                string line;
                while ((line = await reader.ReadLineAsync()) != null) {
                    var fields = line.Split(',');
                    table.Rows.Add(fields);
                }
            }

            using var destConn = new SqlConnection(_prodConnectionString);
            await destConn.OpenAsync();

            using var bulk = new SqlBulkCopy(destConn) {
                DestinationTableName = destinationTable,
                BatchSize = 1000
            };

            foreach (DataColumn col in table.Columns)
                bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);

            await bulk.WriteToServerAsync(table);
            _logger.LogInformation("Imported CSV at {Path} into table {Table}", localPath, destinationTable);
        }
    }
}
