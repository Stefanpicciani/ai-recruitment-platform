using Microsoft.AspNetCore.Http.Features;
using Recruitment.Recruiter.Web.Data;
using Recruitment.Recruiter.Web.Storage;

var builder = WebApplication.CreateBuilder(args);

// Aspire: OpenTelemetry, health checks, service discovery, resilience.
builder.AddServiceDefaults();

// Database: ConnectionStrings:recruiterdb (Azure SQL in Azure, LocalDB or Azure SQL locally).
builder.AddSqlServerDbContext<RecruiterDbContext>("recruiterdb");

// CV storage: ConnectionStrings:resumes -> Azure Blob Storage, otherwise local folder (Development only).
builder.AddResumeStorage();

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

app.Logger.LogInformation("CV storage: {Storage}", app.Services.GetRequiredService<IResumeStorage>().Describe());

await DatabaseInitializer.InitializeAsync(app.Services);

app.Run();
