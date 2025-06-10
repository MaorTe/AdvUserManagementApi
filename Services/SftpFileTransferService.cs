using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace UserManagementApi.Services
{
    /// <summary>
    /// Abstraction for file transfer operations (e.g. SFTP).
    /// </summary>
    public interface IFileTransferService
    {
        Task UploadAsync(string localPath, string remotePath);
        Task DownloadAsync(string remotePath, string localPath);
    }

    /// <summary>
    /// SFTP implementation using SSH.NET (Renci.SshNet).
    /// </summary>
    public class SftpFileTransferService : IFileTransferService, IDisposable
    {
        private readonly SftpClient _client;
        private readonly ILogger<SftpFileTransferService> _logger;

        public SftpFileTransferService(IConfiguration config, ILogger<SftpFileTransferService> logger) {
            var host = config["Sftp:Host"] ?? throw new ArgumentNullException("Sftp:Host");
            var port = config.GetValue<int?>("Sftp:Port") ?? 22;
            var user = config["Sftp:Username"] ?? throw new ArgumentNullException("Sftp:Username");
            var password = config["Sftp:Password"] ?? throw new ArgumentNullException("Sftp:Password");

            _client = new SftpClient(host, port, user, password);
            _logger = logger;
        }

        public async Task UploadAsync(string localPath, string remotePath) {
            if (!_client.IsConnected)
                _client.Connect();

            if (_client.Exists(remotePath)) {
                _client.Delete(remotePath);
                _logger.LogInformation("Deleted existing remote file {RemotePath}", remotePath);
            }

            using var fileStream = File.OpenRead(localPath);
            // Use positional override flag instead of named parameter
            _client.UploadFile(fileStream, remotePath, true);

            _logger.LogInformation("Uploaded local file {LocalPath} to remote {RemotePath}", localPath, remotePath);
            await Task.CompletedTask;
        }

        public async Task DownloadAsync(string remotePath, string localPath) {
            if (!_client.IsConnected)
                _client.Connect();

            if (!_client.Exists(remotePath)) {
                _logger.LogWarning("Remote file {RemotePath} does not exist", remotePath);
                throw new FileNotFoundException($"Remote file not found: {remotePath}");
            }

            using var fileStream = File.OpenWrite(localPath);
            _client.DownloadFile(remotePath, fileStream);

            _logger.LogInformation("Downloaded remote file {RemotePath} to local {LocalPath}", remotePath, localPath);
            await Task.CompletedTask;
        }

        public void Dispose() {
            if (_client.IsConnected)
                _client.Disconnect();
            _client.Dispose();
        }
    }
}
