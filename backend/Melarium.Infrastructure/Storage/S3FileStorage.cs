using Amazon.S3;
using Amazon.S3.Model;
using Melarium.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Melarium.Infrastructure.Storage;

/// <summary>
/// Production file storage on any S3-compatible service (recommended: Cloudflare R2 — free
/// 10 GB, no egress fees). The bucket stays private; files are streamed through the API.
/// Config: <c>Storage:S3:{Bucket, Endpoint, AccessKey, SecretKey}</c> — secrets via env vars only.
/// </summary>
public class S3FileStorage : IFileStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public S3FileStorage(IConfiguration config)
    {
        _bucket = Require(config, "Storage:S3:Bucket");

        var s3Config = new AmazonS3Config
        {
            ServiceURL = Require(config, "Storage:S3:Endpoint"),
            // Path-style addressing works with R2, MinIO and other S3-compatible providers.
            ForcePathStyle = true,
        };

        _client = new AmazonS3Client(
            Require(config, "Storage:S3:AccessKey"),
            Require(config, "Storage:S3:SecretKey"),
            s3Config);
    }

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var key = StoragePathHelper.NewKey(contentType);
        var request = new PutObjectRequest
        {
            BucketName  = _bucket,
            Key         = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            // R2 does not implement S3 payload checksums/chunked signing the same way AWS does.
            DisablePayloadSigning = true,
        };
        await _client.PutObjectAsync(request, cancellationToken);
        return key;
    }

    public async Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, storagePath, cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException($"Stored file not found: {storagePath}", ex);
        }
    }

    public async Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        // S3 DeleteObject is idempotent — no error for missing keys.
        await _client.DeleteObjectAsync(_bucket, storagePath, cancellationToken);
    }

    public void Dispose() => _client.Dispose();

    private static string Require(IConfiguration config, string key) =>
        config[key] ?? throw new InvalidOperationException(
            $"'{key}' is not configured but Storage:Provider is 'S3'. " +
            $"Set the environment variable '{key.Replace(":", "__")}'.");
}
