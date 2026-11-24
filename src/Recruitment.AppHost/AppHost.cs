var builder = DistributedApplication.CreateBuilder(args);

var web = builder.AddDotnetProject("recruiter-web", "../Recruitment.Recruiter.Web/Recruitment.Recruiter.Web.csproj")
    .WithExternalHttpEndpoints();

if (builder.ExecutionContext.IsPublishMode)
{
    // ---- Azure (azd up / aspire deploy) ----
    // Hosts the WebApp on Azure App Service (Linux).
    builder.AddAzureAppServiceEnvironment("appservice");

    // Azure SQL Database (Entra ID auth; the web app's managed identity gets access).
    var sql = builder.AddAzureSqlServer("sql");
    var recruiterDb = sql.AddDatabase("recruiterdb");

    // Private Blob container for the uploaded CVs.
    var storage = builder.AddAzureStorage("storage");
    var resumes = storage.AddBlobContainer("resumes");

    web.WithReference(recruiterDb).WaitFor(recruiterDb)
       .WithReference(resumes);
}
else
{
    // ---- Local development (aspire run / F5) ----
    // No Docker needed: uses the connection string in appsettings / user-secrets
    // (default: SQL Server LocalDB). Point it at the Azure SQL database to test against Azure.
    var recruiterDb = builder.AddConnectionString("recruiterdb");
    web.WithReference(recruiterDb);
    // CVs are stored on the local disk (src/Recruitment.Recruiter.Web/App_Data/resumes).
}

builder.Build().Run();

