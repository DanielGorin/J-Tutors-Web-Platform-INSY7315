using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace J_Tutors_Web_Platform.Services.Storage
{
    // Minimal image-focused blob helper (create container if missing, upload/list/stream/delete)
    public sealed class BlobStorageService
    {
        private readonly BlobContainerClient _container;

        public BlobStorageService(IConfiguration config)
        {
            var cs = config["AzureStorage:ConnectionString"]
                  ?? config.GetConnectionString("StorageAccount");
            var containerName = config["AzureStorage:BlobContainerName"] ?? "images";

            if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("Missing AzureStorage:ConnectionString or ConnectionStrings:StorageAccount.");
            if (string.IsNullOrWhiteSpace(containerName)) throw new InvalidOperationException("Missing AzureStorage:BlobContainerName.");

            var service = new BlobServiceClient(cs.Trim());
            _container = service.GetBlobContainerClient(containerName.Trim().ToLowerInvariant());
            _container.CreateIfNotExists(PublicAccessType.None); // private; we'll stream through the app
        }

        public async Task<string> UploadImageAsync(Stream content, string fileName, string? contentType, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("fileName required");

            // Optional: prevent overwrites by prefixing a timestamp (uncomment if you want)
            // var name = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Path.GetFileName(fileName)}";
            var name = Path.GetFileName(fileName);

            var blob = _container.GetBlobClient(name);

            var headers = new BlobHttpHeaders
            {
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
            };

            await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, ct);
            return name; // store/display this
        }

        public async Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
        {
            var list = new List<string>();
            await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: null, cancellationToken: ct))
            {
                list.Add(item.Name);
            }
            return list;
        }

        // Returns (stream, contentType) for easy File(...) return
        public async Task<(Stream stream, string contentType)> DownloadAsync(string name, CancellationToken ct = default)
        {
            var blob = _container.GetBlobClient(name);
            var resp = await blob.DownloadStreamingAsync(cancellationToken: ct);
            var contentType = resp.Value.Details.ContentType ?? "application/octet-stream";
            return (resp.Value.Content, contentType);
        }

        public async Task<bool> DeleteAsync(string name, CancellationToken ct = default)
        {
            var blob = _container.GetBlobClient(name);
            var resp = await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
            return resp.Value;
        }
    }
}
