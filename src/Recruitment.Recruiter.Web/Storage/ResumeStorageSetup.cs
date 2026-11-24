using Azure.Storage.Blobs;

namespace Recruitment.Recruiter.Web.Storage;

/// <summary>
/// Chooses where CV files are stored.
/// <list type="bullet">
/// <item><b>Azure Blob Storage</b> when <c>ConnectionStrings:resumes</c> is set. Accepted values:
///   a Storage Account connection string (<c>DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...</c>)
///   or the blob endpoint URL (<c>https://&lt;account&gt;.blob.core.windows.net/</c>), which authenticates with
///   Managed Identity in Azure / your Azure login locally (needs role "Storage Blob Data Contributor").
///   Container: <c>ResumeStorage:ContainerName</c> (default "resumes"); created private if missing.</item>
/// <item><b>Local folder</b> otherwise, but only in Development or with <c>ResumeStorage:AllowLocal=true</c>.
///   In any other environment the app refuses to start, so CVs are never silently written to the server disk.</item>
/// </list>
/// </summary>
public static class ResumeStorageSetup
{
    public const string ConnectionName = "resumes";

    public static IHostApplicationBuilder AddResumeStorage(this IHostApplicationBuilder builder)
    {
        var config = builder.Configuration;

        if (!string.IsNullOrWhiteSpace(config.GetConnectionString(ConnectionName)))
        {
            var containerName = config["ResumeStorage:ContainerName"] is { Length: > 0 } c ? c : "resumes";

            // Aspire client: health check, tracing, retries; connection string or service URI + DefaultAzureCredential.
            builder.AddAzureBlobServiceClient(ConnectionName);
            builder.Services.AddSingleton<IResumeStorage>(sp =>
                new BlobResumeStorage(sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(containerName)));
            return builder;
        }

        var allowLocal = config.GetValue<bool?>("ResumeStorage:AllowLocal") ?? builder.Environment.IsDevelopment();
        if (!allowLocal)
        {
            throw new InvalidOperationException(
                "Armazenamento de CVs não configurado: defina ConnectionStrings:resumes (Azure Blob Storage). " +
                "Para usar uma pasta local fora de Development, defina ResumeStorage:AllowLocal=true.");
        }

        builder.Services.AddSingleton<IResumeStorage, FileSystemResumeStorage>();
        return builder;
    }
}
