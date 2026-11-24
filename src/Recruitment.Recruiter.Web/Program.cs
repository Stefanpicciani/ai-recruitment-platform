using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http.Features;
using Recruitment.Recruiter.Web.Data;
using Recruitment.Recruiter.Web.Storage;

var builder = WebApplication.CreateBuilder(args);

// Aspire: OpenTelemetry, health checks, service discovery, resilience.
builder.AddServiceDefaults();

// Azure SQL (connection "recruiterdb" is injected by the Aspire AppHost).
builder.AddSqlServerDbContext<RecruiterDbContext>("recruiterdb");

// CV storage: Azure Blob container "resumes" when configured, local folder otherwise.
if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("resumes")))
{
    builder.AddAzureBlobContainerClient("resumes");
    builder.Services.AddSingleton<IResumeStorage>(sp => new BlobResumeStorage(sp.GetRequiredService<BlobContainerClient>()));
}
else
{
    builder.Services.AddSingleton<IResumeStorage, FileSystemResumeStorage>();
}

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = ResumeRules.MaxFileSizeBytes + 1024 * 1024);
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapDefaultEndpoints();

await DatabaseInitializer.InitializeAsync(app.Services);

app.Run();
