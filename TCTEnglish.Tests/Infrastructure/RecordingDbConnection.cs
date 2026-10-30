using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;

namespace TCTEnglish.Tests.Infrastructure;

/// <summary>
/// In-memory DbConnection seam for sentinel orchestration tests. No network/DML.
/// </summary>
public sealed class RecordingDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;
    private string? _database;
    private readonly List<string> _commands = new();

    public RecordingDbConnection(string? database = "tct_acceptance_s01")
    {
        _database = database;
    }

    public string? SentinelPurpose { get; set; }
    public string? SentinelRunId { get; set; }
    public bool SentinelTableExists { get; set; }
    public IReadOnlyList<string> Commands => new ReadOnlyCollection<string>(_commands);
    public int WriteCommandCount => _commands.Count(IsWrite);

    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => _database ?? string.Empty;
    public override string DataSource => "recording";
    public override string ServerVersion => "10.5.23-mariadb";
    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) => _database = databaseName;
    public override void Close() => _state = ConnectionState.Closed;
    public override void Open() => _state = ConnectionState.Open;
    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        Open();
        return Task.CompletedTask;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => throw new NotSupportedException();

    protected override DbCommand CreateDbCommand() => new RecordingDbCommand(this);

    private static bool IsWrite(string sql)
    {
        var trimmed = sql.TrimStart();
        return trimmed.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("DROP", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("ALTER", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("TRUNCATE", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingDbCommand : DbCommand
    {
        private readonly RecordingDbConnection _connection;

        public RecordingDbCommand(RecordingDbConnection connection)
        {
            _connection = connection;
        }

        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection
        {
            get => _connection;
            set { }
        }
        protected override DbParameterCollection DbParameterCollection { get; } = new RecordingParameterCollection();
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery()
        {
            _connection._commands.Add(CommandText);
            ApplyWrites();
            return 1;
        }

        public override object? ExecuteScalar()
        {
            _connection._commands.Add(CommandText);
            var sql = CommandText;
            if (sql.Contains("SELECT DATABASE()", StringComparison.OrdinalIgnoreCase))
            {
                return _connection._database;
            }

            if (sql.Contains(MariaDbAcceptanceConnectionGuard.SentinelTable, StringComparison.OrdinalIgnoreCase)
                && sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                if (!_connection.SentinelTableExists)
                {
                    throw new InvalidOperationException("Table does not exist.");
                }

                if (sql.Contains("run_id", StringComparison.OrdinalIgnoreCase))
                {
                    return _connection.SentinelRunId;
                }

                if (sql.Contains("purpose", StringComparison.OrdinalIgnoreCase))
                {
                    return _connection.SentinelPurpose;
                }
            }

            return null;
        }

        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new RecordingParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            => throw new NotSupportedException();

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
            => Task.FromResult(ExecuteNonQuery());

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
            => Task.FromResult(ExecuteScalar());

        private void ApplyWrites()
        {
            var sql = CommandText;
            if (sql.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase)
                && sql.Contains(MariaDbAcceptanceConnectionGuard.SentinelTable, StringComparison.OrdinalIgnoreCase))
            {
                _connection.SentinelTableExists = true;
            }

            if (sql.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase)
                && sql.Contains(MariaDbAcceptanceConnectionGuard.SentinelTable, StringComparison.OrdinalIgnoreCase))
            {
                var purpose = FindParameter("@purpose") ?? MariaDbAcceptanceConnectionGuard.ExpectedPurpose;
                var runId = FindParameter("@runId") ?? FindParameter("@run_id");
                if (sql.Contains("ON DUPLICATE KEY UPDATE", StringComparison.OrdinalIgnoreCase)
                    || !_connection.SentinelTableExists
                    || _connection.SentinelPurpose is null)
                {
                    _connection.SentinelPurpose = purpose;
                    _connection.SentinelRunId = runId;
                    _connection.SentinelTableExists = true;
                }
                else if (!string.Equals(_connection.SentinelPurpose, purpose, StringComparison.Ordinal)
                    || (runId is not null && !string.Equals(_connection.SentinelRunId, runId, StringComparison.Ordinal)))
                {
                    // Simulate "do not overwrite" failure for fixed bootstrap; legacy UPSERT path still writes.
                    if (sql.Contains("ON DUPLICATE KEY UPDATE", StringComparison.OrdinalIgnoreCase))
                    {
                        _connection.SentinelPurpose = purpose;
                        _connection.SentinelRunId = runId;
                    }
                }
            }
        }

        private string? FindParameter(string name)
        {
            foreach (RecordingParameter parameter in DbParameterCollection)
            {
                if (string.Equals(parameter.ParameterName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return Convert.ToString(parameter.Value);
                }
            }

            return null;
        }
    }

    private sealed class RecordingParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; } = string.Empty;
        public override int Size { get; set; }
        public override string SourceColumn { get; set; } = string.Empty;
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class RecordingParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = new();
        public override int Count => _items.Count;
        public override object SyncRoot { get; } = new();
        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }
        public override void AddRange(Array values)
        {
            foreach (var value in values)
            {
                Add(value);
            }
        }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => _items.ToArray().CopyTo(array, index);
        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName)
            => _items.FindIndex(item => string.Equals(item.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
            {
                RemoveAt(index);
            }
        }
        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName)
            => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
            {
                _items[index] = value;
            }
            else
            {
                Add(value);
            }
        }
    }
}
