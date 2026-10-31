using System.Reflection;
using MySqlConnector;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>
/// Deterministic MySqlConnector faults for ownership/retry regressions (no live MariaDB needed).
/// </summary>
internal static class MysqlFaultFactory
{
    public static MySqlException Deadlock(string message = "Deadlock found when trying to get lock; try restarting transaction")
        => Create(1213, message);

    public static MySqlException LockWaitTimeout(string message = "Lock wait timeout exceeded; try restarting transaction")
        => Create(1205, message);

    private static MySqlException Create(int number, string message)
    {
        var ctor = typeof(MySqlException).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(MySqlErrorCode), typeof(string)],
            modifiers: null)
            ?? throw new InvalidOperationException("MySqlException(MySqlErrorCode, string) constructor is unavailable.");

        return (MySqlException)ctor.Invoke([(MySqlErrorCode)number, message]);
    }
}
