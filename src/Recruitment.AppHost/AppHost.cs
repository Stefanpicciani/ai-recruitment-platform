var builder = DistributedApplication.CreateBuilder(args);

var web = builder.AddDotnetProject("recruiter-web", "../Recruitment.Recruiter.Web/Recruitment.Recruiter.Web.csproj")
    .WithExternalHttpEndpoints();

if (builder.ExecutionContext.IsPublishMode)
{
    // ---- Azure (azd up / aspire deploy): Aspire provisions everything ----
    // Hosts the WebApp on Azure App Service (Linux).
    builder.AddAzureAppServiceEnvironment("appservice");

    // Azure SQL Database (Entra ID auth; the web app's managed identity gets access).
    var recruiterDb = builder.AddAzureSqlServer("sql").AddDatabase("recruiterdb");

    // Storage Account; the app creates the private "resumes" container on first upload.
    var resumes = builder.AddAzureStorage("storage").AddBlobs("resumes");

    web.WithReference(recruiterDb).WaitFor(recruiterDb)
       .WithReference(resumes);
}
else
{
    // ---- Local run (aspire run / F5): existing resources from configuration ----
    // ConnectionStrings:recruiterdb -> LocalDB (appsettings.json) or Azure SQL (user-secrets).
    web.WithReference(builder.AddConnectionString("recruiterdb"));

    // ConnectionStrings:resumes (user-secrets) -> Azure Blob Storage.
    // Not set -> CVs go to src/Recruitment.Recruiter.Web/App_Data/resumes.
    if (!string.IsNullOrWhiteSpace(builder.Configuration["ConnectionStrings:resumes"]))
    {
        web.WithReference(builder.AddConnectionString("resumes"));
    }

    if (builder.Configuration["ResumeStorage:ContainerName"] is { Length: > 0 } container)
    {
        web.WithEnvironment("ResumeStorage__ContainerName", container);
    }
}

builder.Build().Run();
