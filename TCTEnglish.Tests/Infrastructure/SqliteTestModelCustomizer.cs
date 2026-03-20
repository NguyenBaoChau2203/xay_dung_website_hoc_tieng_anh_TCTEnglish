using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace TCTEnglish.Tests.Infrastructure;

public sealed class SqliteTestModelCustomizer : ModelCustomizer
{
    public SqliteTestModelCustomizer(ModelCustomizerDependencies dependencies)
        : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var columnType = property.GetColumnType();
                if (!string.IsNullOrWhiteSpace(columnType)
                    && columnType.Contains("(max)", StringComparison.OrdinalIgnoreCase))
                {
                    property.SetColumnType(columnType.Replace("(max)", string.Empty, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(property.GetDefaultValueSql()))
                {
                    property.SetDefaultValueSql(null);
                }
            }
        }
    }
}
