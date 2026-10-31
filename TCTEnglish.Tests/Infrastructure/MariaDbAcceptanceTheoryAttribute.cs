using Xunit;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>
/// Theory counterpart of <see cref="MariaDbAcceptanceFactAttribute"/>.
/// </summary>
public sealed class MariaDbAcceptanceTheoryAttribute : TheoryAttribute
{
    public MariaDbAcceptanceTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")))
        {
            Skip = "Not run: set TCT_ACCEPTANCE_MARIADB to an isolated MariaDB acceptance connection string.";
        }
    }
}
