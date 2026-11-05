using System.Data;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Infrastructure.SharedData;

internal sealed class SharedDataDatabaseLock(DbflashcardContext db, bool close, string? name) : IAsyncDisposable
{
    public static async Task<SharedDataDatabaseLock> AcquireAsync(DbflashcardContext db, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection(); var close = connection.State != ConnectionState.Open;
        if (close) await db.Database.OpenConnectionAsync(ct);
        string? name = null;
        if (db.Database.IsMySql())
        {
            name = "tct.shared." + SharedDataFormat.Hash(System.Text.Encoding.UTF8.GetBytes(connection.Database))[..24];
            await using var command = connection.CreateCommand(); command.CommandText = "SELECT GET_LOCK(@name,30)";
            var p = command.CreateParameter(); p.ParameterName = "@name"; p.Value = name; command.Parameters.Add(p);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
            {
                if (close) await db.Database.CloseConnectionAsync();
                throw new SharedDataException("Another shared-data operation is running.");
            }
        }
        return new(db, close, name);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (name != null)
            {
                await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT RELEASE_LOCK(@name)";
                var p = command.CreateParameter(); p.ParameterName = "@name"; p.Value = name; command.Parameters.Add(p);
                await command.ExecuteScalarAsync();
            }
        }
        finally { if (close) await db.Database.CloseConnectionAsync(); }
    }
}
