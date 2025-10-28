using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Files.Shares;

namespace J_Tutors_Web_Platform.Services.Storage
{
    public sealed class FileShareService
    {
        private readonly ShareClient _share;

        public FileShareService(Microsoft.Extensions.Configuration.IConfiguration config)
        {
            var cs = config["AzureStorage:ConnectionString"]
                  ?? config.GetConnectionString("StorageAccount");
            var shareName = config["AzureStorage:FileShareName"] ?? "jtutors-fileshare";
            if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("Missing storage connection string.");
            if (string.IsNullOrWhiteSpace(shareName)) throw new InvalidOperationException("Missing file share name.");

            // Pin to a stable API version to avoid odd 405s from future versions
            var opts = new ShareClientOptions(ShareClientOptions.ServiceVersion.V2023_11_03);

            _share = new ShareClient(cs.Trim(), shareName.Trim(), opts);
            _share.CreateIfNotExists(); // idempotent
        }

        // ROOT ONLY
        public async Task<string> UploadAsync(Stream content, long length, string fileName, CancellationToken ct = default)
        {
            if (length <= 0) throw new ArgumentException("length must be > 0");
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("fileName required");

            var root = _share.GetRootDirectoryClient();
            await root.CreateIfNotExistsAsync(cancellationToken: ct);

            var file = root.GetFileClient(fileName.Trim());
            await file.CreateAsync(length, cancellationToken: ct);
            await file.UploadRangeAsync(new Azure.HttpRange(0, length), content, cancellationToken: ct);

            return file.Name; // just the name in root
        }

        public async Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
        {
            var root = _share.GetRootDirectoryClient();
            await root.CreateIfNotExistsAsync(cancellationToken: ct);

            var list = new List<string>();
            await foreach (var item in root.GetFilesAndDirectoriesAsync(cancellationToken: ct))
                if (!item.IsDirectory) list.Add(item.Name);
            return list;
        }

        public async Task<Stream> DownloadAsync(string fileName, CancellationToken ct = default)
        {
            var root = _share.GetRootDirectoryClient();
            await root.CreateIfNotExistsAsync(cancellationToken: ct);

            var file = root.GetFileClient((fileName ?? "").Trim());
            var resp = await file.DownloadAsync(cancellationToken: ct);
            var ms = new MemoryStream();
            await resp.Value.Content.CopyToAsync(ms, ct);
            ms.Position = 0;
            return ms;
        }
    }
}
