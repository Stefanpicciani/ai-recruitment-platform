using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Recruitment.Recruiter.Web.Data;

/// <summary>
/// Startup initialization of the Recruiter database. Never drops or deletes anything:
/// <list type="number">
/// <item>Database: if it already exists it is used as is. If it doesn't exist, it is created,
/// or, when its .mdf file is already on disk (e.g. LocalDB lost its registration), that file is re-attached.</item>
/// <item>Schema: Data/Sql/schema.sql is applied only when dbo.JobOpportunities does not exist.</item>
/// </list>
/// Controlled by "Database:ApplySchemaOnStartup" (default true).
/// </summary>
public static partial class DatabaseInitializer
{
    private const int CannotOpenDatabase = 4060;
    private const int AzureSqlDatabaseEdition = 5;

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (!config.GetValue("Database:ApplySchemaOnStartup", true))
        {
            logger.LogInformation("Database initialization disabled (Database:ApplySchemaOnStartup=false).");
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<RecruiterDbContext>();
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Connection string 'recruiterdb' is not configured.");

        await EnsureDatabaseAsync(connectionString, logger, ct);
        await EnsureSchemaAsync(db, logger, ct);
    }

    // ---------------------------------------------------------------- database

    private static async Task EnsureDatabaseAsync(string connectionString, ILogger logger, CancellationToken ct)
    {
        // 1) Can we open the database itself? Then it exists – nothing to do.
        //    (On Azure SQL this is always the path taken: the app identity may not even have access to master.)
        try
        {
            await using var probe = new SqlConnection(connectionString);
            await probe.OpenAsync(ct);
            logger.LogInformation("Database '{Database}' already exists.", probe.Database);
            return;
        }
        catch (SqlException ex) when (ex.Errors.Cast<SqlError>().Any(e => e.Number == CannotOpenDatabase))
        {
            // Database not found on the server: continue below.
        }

        var target = new SqlConnectionStringBuilder(connectionString);
        var dbName = target.InitialCatalog;
        if (string.IsNullOrWhiteSpace(dbName))
            throw new InvalidOperationException("The connection string has no database name (Initial Catalog).");

        SqlConnection.ClearPool(new SqlConnection(connectionString));

        var masterCs = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        if (masterCs.ContainsKey("AttachDBFilename")) masterCs.Remove("AttachDBFilename");

        await using var master = new SqlConnection(masterCs.ConnectionString);
        await master.OpenAsync(ct);

        // 2) Double-check in master (another process may have created it in the meantime).
        if (await ScalarAsync<int?>(master, "SELECT DB_ID(@name)", ct, ("@name", dbName)) is not null)
        {
            logger.LogInformation("Database '{Database}' already exists.", dbName);
            return;
        }

        var quotedName = await ScalarAsync<string>(master, "SELECT QUOTENAME(@name)", ct, ("@name", dbName));
        var edition = await ScalarAsync<int>(master, "SELECT CAST(SERVERPROPERTY('EngineEdition') AS int)", ct);

        // 3a) Azure SQL Database: no file management, plain CREATE DATABASE.
        if (edition == AzureSqlDatabaseEdition)
        {
            logger.LogInformation("Creating database '{Database}' on Azure SQL.", dbName);
            await ExecAsync(master, $"IF DB_ID(@name) IS NULL CREATE DATABASE {quotedName}", ct, ("@name", dbName));
            return;
        }

        // 3b) SQL Server / LocalDB: look at the files in the instance's default folders.
        var dataDir = await ScalarAsync<string>(master, "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000))", ct);
        var logDir = await ScalarAsync<string>(master, "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000))", ct);
        var mdf = Path.Combine(dataDir, $"{dbName}.mdf");
        var ldf = Path.Combine(logDir, $"{dbName}_log.ldf");
        var mdfExists = File.Exists(mdf);
        var ldfExists = File.Exists(ldf);

        string sql;
        if (mdfExists && ldfExists)
        {
            logger.LogWarning("Database '{Database}' is not registered but its files exist. Attaching {Mdf} and {Ldf}.", dbName, mdf, ldf);
            sql = $"IF DB_ID(@name) IS NULL CREATE DATABASE {quotedName} ON (FILENAME = {Literal(mdf)}), (FILENAME = {Literal(ldf)}) FOR ATTACH";
        }
        else if (mdfExists)
        {
            logger.LogWarning("Database '{Database}' is not registered and only {Mdf} exists. Attaching and rebuilding the log file.", dbName, mdf);
            sql = $"IF DB_ID(@name) IS NULL CREATE DATABASE {quotedName} ON (FILENAME = {Literal(mdf)}) FOR ATTACH_REBUILD_LOG";
        }
        else if (ldfExists)
        {
            // A log file without its data file cannot be attached, and creating the database would collide with it.
            // We never delete files: stop and let the developer decide.
            throw new InvalidOperationException(
                $"Database '{dbName}' does not exist and '{ldf}' exists without its data file '{mdf}'. " +
                "Move or delete that log file manually, then restart the application.");
        }
        else
        {
            logger.LogInformation("Creating database '{Database}' ({Mdf}).", dbName, mdf);
            sql = $"IF DB_ID(@name) IS NULL CREATE DATABASE {quotedName} " +
                  $"ON PRIMARY (NAME = {Literal(dbName)}, FILENAME = {Literal(mdf)}) " +
                  $"LOG ON (NAME = {Literal(dbName + "_log")}, FILENAME = {Literal(ldf)})";
        }

        await ExecAsync(master, sql, ct, ("@name", dbName));
        logger.LogInformation("Database '{Database}' is ready.", dbName);
    }

    // ---------------------------------------------------------------- schema

    private static async Task EnsureSchemaAsync(RecruiterDbContext db, ILogger logger, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var exists = await db.Database
                .SqlQueryRaw<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.JobOpportunities', N'U') IS NULL THEN 0 ELSE 1 END AS [Value]")
                .SingleAsync(ct);

            if (exists == 1)
            {
                logger.LogInformation("Recruiter schema already present.");
                return;
            }

            logger.LogInformation("Applying Recruiter schema (schema.sql).");
            var script = await ReadSchemaAsync();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            foreach (var batch in GoSeparator().Split(script).Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                await db.Database.ExecuteSqlRawAsync(batch, ct);
            }
            await tx.CommitAsync(ct);
            logger.LogInformation("Recruiter schema applied.");
        });
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>N'...' literal with quotes escaped (CREATE DATABASE does not accept parameters for names/paths).</summary>
    private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";

    private static async Task<T> ScalarAsync<T>(SqlConnection conn, string sql, CancellationToken ct, params (string Name, object Value)[] args)
    {
        await using var cmd = Command(conn, sql, args);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null or DBNull ? default! : (T)result;
    }

    private static async Task ExecAsync(SqlConnection conn, string sql, CancellationToken ct, params (string Name, object Value)[] args)
    {
        await using var cmd = Command(conn, sql, args);
        cmd.CommandTimeout = 120;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static SqlCommand Command(SqlConnection conn, string sql, (string Name, object Value)[] args)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        return cmd;
    }

    private static async Task<string> ReadSchemaAsync()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith("schema.sql", StringComparison.Ordinal));
        await using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}
