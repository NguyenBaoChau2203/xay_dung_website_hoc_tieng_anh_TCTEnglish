using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TCTVocabulary.Models;

namespace TCTEnglish.Infrastructure;

/// <summary>EF tooling must never start the web host, workers or seeders.</summary>
public sealed class DbflashcardContextFactory : IDesignTimeDbContextFactory<DbflashcardContext>
{
    public DbflashcardContext CreateDbContext(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var basePath = File.Exists(Path.Combine(currentDirectory, "appsettings.json"))
            ? currentDirectory
            : Path.Combine(currentDirectory, "TCTEnglish");
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";
        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true);
        var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
        if (isDevelopment)
            configurationBuilder.AddUserSecrets<DbflashcardContextFactory>(optional: true);

        var configuration = configurationBuilder.AddEnvironmentVariables().Build();
        var settings = DatabaseSettings.Read(configuration, isDevelopment);
        return new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(settings.ConnectionString, settings.ServerVersion).Options);
    }
}
