using Microsoft.Data.Sqlite;

namespace ScreentimeTracker.Core;

/// <summary>
/// Sources and the time records attributed to them, in a SQLite database file. Times are stored as
/// local-time ticks.
/// </summary>
internal sealed class SqliteStore : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteStore(string databasePath)
    {
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        _connection.Open();

        Execute("""
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS sources (
                id           INTEGER PRIMARY KEY,
                kind         TEXT    NOT NULL,
                key          TEXT    NOT NULL,
                display_name TEXT    NOT NULL,
                UNIQUE (kind, key)
            );
            CREATE TABLE IF NOT EXISTS time_records (
                id         INTEGER PRIMARY KEY,
                source_id  INTEGER NOT NULL REFERENCES sources (id),
                start_time INTEGER NOT NULL,
                end_time   INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS time_records_by_end ON time_records (end_time);
            CREATE TABLE IF NOT EXISTS settings (
                name  TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """);

        // Databases created before Categories existed lack the column; their Sources start Uncategorized.
        if (!HasColumn("sources", "category"))
        {
            Execute($"ALTER TABLE sources ADD COLUMN category TEXT NOT NULL DEFAULT '{Category.Uncategorized}';");
        }
    }

    public string? ReadSetting(string name)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE name = $name;";
        command.Parameters.AddWithValue("$name", name);
        return command.ExecuteScalar() as string;
    }

    public void WriteSetting(string name, string value)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings (name, value) VALUES ($name, $value)
            ON CONFLICT (name) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    /// <summary>Records the Source, refreshing its display name, and returns its id.</summary>
    public long SaveSource(SourceKind kind, string key, string displayName)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sources (kind, key, display_name) VALUES ($kind, $key, $name)
            ON CONFLICT (kind, key) DO UPDATE SET display_name = excluded.display_name
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$kind", kind.ToString());
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$name", displayName);
        return (long)command.ExecuteScalar()!;
    }

    public void SetCategory(SourceKind kind, string key, Category category)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "UPDATE sources SET category = $category WHERE kind = $kind AND key = $key;";
        command.Parameters.AddWithValue("$category", category.ToString());
        command.Parameters.AddWithValue("$kind", kind.ToString());
        command.Parameters.AddWithValue("$key", key);
        command.ExecuteNonQuery();
    }

    public long StartRecord(long sourceId, DateTime start)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO time_records (source_id, start_time, end_time) VALUES ($source, $start, $start)
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$source", sourceId);
        command.Parameters.AddWithValue("$start", start.Ticks);
        return (long)command.ExecuteScalar()!;
    }

    public void SetRecordEnd(long recordId, DateTime end)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "UPDATE time_records SET end_time = $end WHERE id = $id;";
        command.Parameters.AddWithValue("$end", end.Ticks);
        command.Parameters.AddWithValue("$id", recordId);
        command.ExecuteNonQuery();
    }

    /// <summary>Screen Time per Source within [from, to), clipping records that cross either edge.</summary>
    public IReadOnlyList<SourceTime> SourcesBetween(DateTime from, DateTime to)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT s.kind, s.key, s.display_name, s.category,
                   SUM(MIN(r.end_time, $to) - MAX(r.start_time, $from)) AS total
            FROM time_records r
            JOIN sources s ON s.id = r.source_id
            WHERE r.end_time > $from AND r.start_time < $to
            GROUP BY s.id
            HAVING total > 0
            ORDER BY total DESC, s.display_name;
            """;
        command.Parameters.AddWithValue("$from", from.Ticks);
        command.Parameters.AddWithValue("$to", to.Ticks);

        var result = new List<SourceTime>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new SourceTime(
                Enum.Parse<SourceKind>(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                Enum.Parse<Category>(reader.GetString(3)),
                TimeSpan.FromTicks(reader.GetInt64(4))));
        }
        return result;
    }

    public void Dispose() => _connection.Dispose();

    private bool HasColumn(string table, string column)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name = $column;";
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$column", column);
        return (long)command.ExecuteScalar()! > 0;
    }

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
