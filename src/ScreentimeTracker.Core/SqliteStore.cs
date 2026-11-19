using Microsoft.Data.Sqlite;

namespace ScreentimeTracker.Core;

/// <summary>
/// Sources and the time records attributed to them, in a SQLite database file. Times are stored as
/// local-time ticks.
/// </summary>
internal sealed class SqliteStore : IDisposable
{
    private const string AppKind = "App";

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
            """);
    }

    /// <summary>Records the App as a Source, refreshing its display name, and returns its id.</summary>
    public long SaveApp(string key, string displayName)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sources (kind, key, display_name) VALUES ($kind, $key, $name)
            ON CONFLICT (kind, key) DO UPDATE SET display_name = excluded.display_name
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$kind", AppKind);
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$name", displayName);
        return (long)command.ExecuteScalar()!;
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
            SELECT s.key, s.display_name,
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
            result.Add(new SourceTime(reader.GetString(0), reader.GetString(1), TimeSpan.FromTicks(reader.GetInt64(2))));
        }
        return result;
    }

    public void Dispose() => _connection.Dispose();

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
