using System.Collections;
using System.Data;
using System.Data.Common;
using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class MariaDbFreshBaselineGuardTests
{
    [Fact]
    public async Task RequireFreshBaselineTargetAsync_AllowsSentinelOnly()
    {
        await using var connection = new FakeAcceptanceConnection(
            database: "tct_acceptance_guard",
            purpose: MariaDbAcceptanceConnectionGuard.ExpectedPurpose,
            runId: "guard_run",
            historyRowCount: 0,
            existingTables: [MariaDbAcceptanceConnectionGuard.SentinelTable]);

        await MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(
            connection,
            "tct_acceptance_guard",
            "guard_run");

        Assert.Equal(0, connection.MutatingStatements);
    }

    [Fact]
    public async Task RequireFreshBaselineTargetAsync_AllowsEmptyMigrationsHistoryMetadata()
    {
        await using var connection = new FakeAcceptanceConnection(
            database: "tct_acceptance_guard",
            purpose: MariaDbAcceptanceConnectionGuard.ExpectedPurpose,
            runId: "guard_run",
            historyRowCount: 0,
            existingTables:
            [
                MariaDbAcceptanceConnectionGuard.SentinelTable,
                "__EFMigrationsHistory"
            ]);

        await MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(
            connection,
            "tct_acceptance_guard",
            "guard_run");

        Assert.Equal(0, connection.MutatingStatements);
    }

    [Fact]
    public async Task RequireFreshBaselineTargetAsync_RejectsWhenMigrationsHistoryHasRows()
    {
        await using var connection = new FakeAcceptanceConnection(
            database: "tct_acceptance_guard",
            purpose: MariaDbAcceptanceConnectionGuard.ExpectedPurpose,
            runId: "guard_run",
            historyRowCount: 3,
            existingTables:
            [
                MariaDbAcceptanceConnectionGuard.SentinelTable,
                "__EFMigrationsHistory"
            ]);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(
                connection,
                "tct_acceptance_guard",
                "guard_run"));

        Assert.Contains("__EFMigrationsHistory", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, connection.MutatingStatements);
    }

    [Theory]
    [InlineData("Sets")]
    [InlineData("Cards")]
    [InlineData("SpeakingVideos")]
    [InlineData("custom_leftover")]
    [InlineData("WritingAcceptedAnswers")]
    public async Task RequireFreshBaselineTargetAsync_RejectsAnyNonMetadataTable_EvenWithEmptyHistory(
        string leftoverTable)
    {
        await using var connection = new FakeAcceptanceConnection(
            database: "tct_acceptance_guard",
            purpose: MariaDbAcceptanceConnectionGuard.ExpectedPurpose,
            runId: "guard_run",
            historyRowCount: 0,
            existingTables:
            [
                MariaDbAcceptanceConnectionGuard.SentinelTable,
                leftoverTable
            ]);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(
                connection,
                "tct_acceptance_guard",
                "guard_run"));

        Assert.Contains(leftoverTable, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allowlist", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, connection.MutatingStatements);
    }

    private sealed class FakeAcceptanceConnection : DbConnection
    {
        private readonly string _database;
        private readonly string _purpose;
        private readonly string _runId;
        private readonly int _historyRowCount;
        private readonly List<string> _existingTables;
        private ConnectionState _state = ConnectionState.Closed;

        public FakeAcceptanceConnection(
            string database,
            string purpose,
            string runId,
            int historyRowCount,
            IEnumerable<string> existingTables)
        {
            _database = database;
            _purpose = purpose;
            _runId = runId;
            _historyRowCount = historyRowCount;
            _existingTables = existingTables
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public int MutatingStatements { get; private set; }

        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => _database;
        public override string DataSource => "127.0.0.1";
        public override string ServerVersion => "10.5.23-mariadb";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;
        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            _state = ConnectionState.Open;
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new FakeAcceptanceCommand(this);

        private sealed class FakeAcceptanceCommand : DbCommand
        {
            private readonly FakeAcceptanceConnection _connection;

            public FakeAcceptanceCommand(FakeAcceptanceConnection connection)
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
            protected override DbParameterCollection DbParameterCollection { get; } = new FakeParameterCollection();
            protected override DbTransaction? DbTransaction { get; set; }

            public override void Cancel() { }
            public override int ExecuteNonQuery()
            {
                _connection.MutatingStatements++;
                return 0;
            }

            public override object? ExecuteScalar()
            {
                var sql = CommandText ?? string.Empty;
                if (sql.Contains("SELECT DATABASE()", StringComparison.OrdinalIgnoreCase))
                {
                    return _connection._database;
                }

                if (sql.Contains($"FROM `{MariaDbAcceptanceConnectionGuard.SentinelTable}`", StringComparison.Ordinal)
                    && sql.Contains("purpose", StringComparison.OrdinalIgnoreCase))
                {
                    return _connection._purpose;
                }

                if (sql.Contains($"FROM `{MariaDbAcceptanceConnectionGuard.SentinelTable}`", StringComparison.Ordinal)
                    && sql.Contains("run_id", StringComparison.OrdinalIgnoreCase))
                {
                    return _connection._runId;
                }

                if (sql.Contains("FROM `__EFMigrationsHistory`", StringComparison.Ordinal))
                {
                    return _connection._historyRowCount;
                }

                if (sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
                    || sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                    || sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase)
                    || sql.Contains("DROP", StringComparison.OrdinalIgnoreCase)
                    || sql.Contains("CREATE", StringComparison.OrdinalIgnoreCase)
                    || sql.Contains("ALTER", StringComparison.OrdinalIgnoreCase))
                {
                    _connection.MutatingStatements++;
                }

                return 0;
            }

            public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
                => Task.FromResult(ExecuteScalar());

            public override void Prepare() { }
            protected override DbParameter CreateDbParameter() => new FakeParameter();

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            {
                var sql = CommandText ?? string.Empty;
                if (sql.Contains("information_schema.TABLES", StringComparison.OrdinalIgnoreCase)
                    && sql.Contains("TABLE_NAME", StringComparison.OrdinalIgnoreCase)
                    && !sql.Contains("COUNT(*)", StringComparison.OrdinalIgnoreCase))
                {
                    return new FakeTableListReader(_connection._existingTables);
                }

                throw new NotSupportedException($"Unexpected reader SQL: {sql}");
            }

            protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
                CommandBehavior behavior,
                CancellationToken cancellationToken)
                => Task.FromResult(ExecuteDbDataReader(behavior));
        }

        private sealed class FakeTableListReader : DbDataReader
        {
            private readonly IReadOnlyList<string> _tables;
            private int _index = -1;

            public FakeTableListReader(IReadOnlyList<string> tables)
            {
                _tables = tables;
            }

            public override bool Read()
            {
                _index++;
                return _index < _tables.Count;
            }

            public override Task<bool> ReadAsync(CancellationToken cancellationToken)
                => Task.FromResult(Read());

            public override string GetString(int ordinal) => _tables[_index];
            public override int FieldCount => 1;
            public override bool HasRows => _tables.Count > 0;
            public override bool IsClosed => false;
            public override int RecordsAffected => 0;
            public override int Depth => 0;
            public override object this[int ordinal] => GetString(ordinal);
            public override object this[string name] => GetString(0);
            public override object GetValue(int ordinal) => GetString(ordinal);
            public override int GetValues(object[] values)
            {
                values[0] = GetString(0);
                return 1;
            }
            public override string GetName(int ordinal) => "TABLE_NAME";
            public override int GetOrdinal(string name) => 0;
            public override Type GetFieldType(int ordinal) => typeof(string);
            public override string GetDataTypeName(int ordinal) => "varchar";
            public override bool IsDBNull(int ordinal) => false;
            public override bool NextResult() => false;
            public override void Close() { }
            public override DataTable GetSchemaTable() => throw new NotSupportedException();
            public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
            public override byte GetByte(int ordinal) => throw new NotSupportedException();
            public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
                => throw new NotSupportedException();
            public override char GetChar(int ordinal) => throw new NotSupportedException();
            public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
                => throw new NotSupportedException();
            public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
            public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
            public override double GetDouble(int ordinal) => throw new NotSupportedException();
            public override float GetFloat(int ordinal) => throw new NotSupportedException();
            public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
            public override short GetInt16(int ordinal) => throw new NotSupportedException();
            public override int GetInt32(int ordinal) => throw new NotSupportedException();
            public override long GetInt64(int ordinal) => throw new NotSupportedException();
            public override IEnumerator GetEnumerator() => _tables.GetEnumerator();
        }

        private sealed class FakeParameter : DbParameter
        {
            public override DbType DbType { get; set; }
            public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
            public override bool IsNullable { get; set; }
            public override string ParameterName { get; set; } = string.Empty;
            public override string SourceColumn { get; set; } = string.Empty;
            public override object? Value { get; set; }
            public override bool SourceColumnNullMapping { get; set; }
            public override int Size { get; set; }
            public override void ResetDbType() { }
        }

        private sealed class FakeParameterCollection : DbParameterCollection
        {
            private readonly List<DbParameter> _items = new();
            public override int Count => _items.Count;
            public override object SyncRoot { get; } = new();
            public override int Add(object value)
            {
                _items.Add((DbParameter)value);
                return _items.Count - 1;
            }
            public override void AddRange(Array values) => _items.AddRange(values.Cast<DbParameter>());
            public override void Clear() => _items.Clear();
            public override bool Contains(object value) => _items.Contains((DbParameter)value);
            public override bool Contains(string value) => _items.Any(item => item.ParameterName == value);
            public override void CopyTo(Array array, int index) => _items.ToArray().CopyTo(array, index);
            public override IEnumerator GetEnumerator() => _items.GetEnumerator();
            public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
            public override int IndexOf(string parameterName)
                => _items.FindIndex(item => item.ParameterName == parameterName);
            public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
            public override void Remove(object value) => _items.Remove((DbParameter)value);
            public override void RemoveAt(int index) => _items.RemoveAt(index);
            public override void RemoveAt(string parameterName)
            {
                var index = IndexOf(parameterName);
                if (index >= 0) RemoveAt(index);
            }
            protected override DbParameter GetParameter(int index) => _items[index];
            protected override DbParameter GetParameter(string parameterName)
                => _items[IndexOf(parameterName)];
            protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
            protected override void SetParameter(string parameterName, DbParameter value)
            {
                var index = IndexOf(parameterName);
                if (index >= 0) _items[index] = value;
                else Add(value);
            }
        }
    }
}
