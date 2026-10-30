using Xunit;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>
/// Runs only when TCT_ACCEPTANCE_MARIADB points at an isolated acceptance DB.
/// Missing provider is Skip (Not run), not a silent pass of the concurrency assertion.
/// </summary>
public sealed class MariaDbAcceptanceFactAttribute : FactAttribute
{
    public MariaDbAcceptanceFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")))
        {
            Skip = "Not run: set TCT_ACCEPTANCE_MARIADB to an isolated MariaDB acceptance connection string.";
        }
    }
}
