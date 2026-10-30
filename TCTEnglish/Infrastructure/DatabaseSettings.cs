using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace TCTEnglish.Infrastructure;

public sealed record DatabaseSettings(string ConnectionString, ServerVersion ServerVersion)
{
    public static DatabaseSettings Read(IConfiguration configuration, bool isDevelopment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Configure local User Secrets with scripts/start-local.ps1, or set ConnectionStrings__DefaultConnection on the deployment host.");
        }

        var parsed = new MySqlConnectionStringBuilder(connectionString);
        if (isDevelopment && parsed.Server is not ("127.0.0.1" or "localhost" or "::1"))
        {
            throw new InvalidOperationException("Development must use a local database. Run scripts/start-local.ps1 to configure Docker and User Secrets.");
        }

        var version = configuration["Database:ServerVersion"] ?? "10.5.23-mariadb";
        return new DatabaseSettings(connectionString, ServerVersion.Parse(version));
    }
}
