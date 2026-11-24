using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Recruitment.Recruiter.Web.Storage;

public record StoredResume(string StoredFileName, string StoredPath);

/// <summary>
/// Stores CV files. StoredPath is what goes into the database.
/// Implementations never store empty content: <see cref="SaveAsync"/> throws instead.
/// </summary>
public interface IResumeStorage
{
    Task<StoredResume> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string storedPath, CancellationToken ct = default);

    /// <summary>Removes a stored file (used to roll back when the database save fails). No-op if it doesn't exist.</summary>
    Task DeleteAsync(string storedPath, CancellationToken ct = default);
}

/// <summary>Azure Blob Storage (used when the "resumes" blob connection is configured, e.g. in Azure).</summary>
public sealed class BlobResumeStorage(BlobContainerClient container) : IResumeStorage
{
    private bool _ensured;

    public async Task<StoredResume> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default)
    {
        ResumeFileNames.EnsureNotEmpty(content);

        if (!_ensured)
        {
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            _ensured = true;
        }

        var storedName = ResumeFileNames.NewStoredName(originalFileName);
        var blobName = $"{DateTime.UtcNow:yyyy/MM}/{storedName}";
        var blob = container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct);
        return new StoredResume(storedName, $"{container.Name}/{blobName}");
    }

    public async Task<Stream?> OpenReadAsync(string storedPath, CancellationToken ct = default)
    {
        if (BlobName(storedPath) is not { } name) return null;
        var blob = container.GetBlobClient(name);
        if (!await blob.ExistsAsync(ct)) return null;
        return await blob.OpenReadAsync(cancellationToken: ct);
    }

    public async Task DeleteAsync(string storedPath, CancellationToken ct = default)
    {
        if (BlobName(storedPath) is { } name)
            await container.GetBlobClient(name).DeleteIfExistsAsync(cancellationToken: ct);
    }

    private string? BlobName(string storedPath)
    {
        var prefix = container.Name + "/";
        return storedPath.StartsWith(prefix, StringComparison.Ordinal) ? storedPath[prefix.Length..] : null;
    }
}

/// <summary>Local folder storage, for local development without Azure Storage.</summary>
public sealed class FileSystemResumeStorage(IConfiguration config, IWebHostEnvironment env) : IResumeStorage
{
    private const string Prefix = "local/";
    private readonly string _root = Path.GetFullPath(config["ResumeStorage:LocalPath"] ?? Path.Combine(env.ContentRootPath, "App_Data", "resumes"));

    public async Task<StoredResume> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default)
    {
        ResumeFileNames.EnsureNotEmpty(content);

        Directory.CreateDirectory(_root);
        var storedName = ResumeFileNames.NewStoredName(originalFileName);
        var fullPath = Path.Combine(_root, storedName);

        long written;
        await using (var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
        {
            await content.CopyToAsync(file, ct);
            written = file.Length;
        }

        // Non-seekable streams can't be checked up front: never leave an empty file behind.
        if (written == 0)
        {
            File.Delete(fullPath);
            throw new InvalidOperationException("O ficheiro do currículo está vazio.");
        }

        return new StoredResume(storedName, Prefix + storedName);
    }

    public Task<Stream?> OpenReadAsync(string storedPath, CancellationToken ct = default)
    {
        var full = FullPath(storedPath);
        return Task.FromResult<Stream?>(full is not null && File.Exists(full) ? File.OpenRead(full) : null);
    }

    public Task DeleteAsync(string storedPath, CancellationToken ct = default)
    {
        var full = FullPath(storedPath);
        if (full is not null && File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    private string? FullPath(string storedPath) =>
        storedPath.StartsWith(Prefix, StringComparison.Ordinal)
            ? Path.Combine(_root, Path.GetFileName(storedPath[Prefix.Length..]))
            : null;
}

public static class ResumeFileNames
{
    public static string NewStoredName(string originalFileName)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        return $"{Guid.NewGuid():N}{ext}";
    }

    public static void EnsureNotEmpty(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.CanSeek && content.Length - content.Position <= 0)
            throw new InvalidOperationException("O ficheiro do currículo está vazio.");
    }
}
