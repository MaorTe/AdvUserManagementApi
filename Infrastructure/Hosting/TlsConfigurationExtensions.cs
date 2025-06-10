using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;

namespace UserManagementApi.Infrastructure.Hosting;

public static class TlsConfigurationExtensions
{
    /// <summary>
    /// Configures Kestrel to use HTTPS with TLS 1.2+ and loads a PFX certificate from configuration.
    /// Does not bind ports; endpoints are configured separately (e.g., via launchSettings or environment variables).
    /// </summary>
    /// <param name="options">Kestrel server options to configure.</param>
    /// <param name="config">Application configuration containing Certificate:Path and Certificate:Password.</param>
    public static void ConfigureTls(this KestrelServerOptions options, IConfiguration config) {
        // Load certificate settings
        var certPath = config["Certificate:Path"];
        var certPassword = config["Certificate:Password"];
        if (string.IsNullOrEmpty(certPath) || string.IsNullOrEmpty(certPassword))
            throw new InvalidOperationException(
                "Certificate path or password not configured. Please set Certificate:Path and Certificate:Password in configuration.");

        // Load PFX into memory
        var certificate = new X509Certificate2(
            certPath,
            certPassword,
            X509KeyStorageFlags.EphemeralKeySet);

        // Apply certificate and protocols to all HTTPS endpoints
        options.ConfigureHttpsDefaults(httpsOptions => {
            httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
            httpsOptions.ServerCertificate = certificate;
        });
    }
}
