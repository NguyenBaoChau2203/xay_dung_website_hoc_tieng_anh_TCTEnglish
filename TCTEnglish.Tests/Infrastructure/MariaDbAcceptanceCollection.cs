using Xunit;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>
/// Serializes all MariaDB acceptance facts that share TCT_ACCEPTANCE_MARIADB.
/// </summary>
[CollectionDefinition("MariaDbAcceptance", DisableParallelization = true)]
public sealed class MariaDbAcceptanceCollection
{
}
