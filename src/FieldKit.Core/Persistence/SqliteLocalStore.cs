using System.Diagnostics;
using FieldKit.Core.Services;
using Microsoft.Data.Sqlite;

namespace FieldKit.Core.Persistence;

/// <summary>
/// <see cref="ILocalStore"/> on SQLite (<c>fieldkit.db</c> in <c>%LOCALAPPDATA%\FieldKit</c>, so it's
/// per user). Plain SQL, one connection, versioned with <c>user_version</c>.
/// </summary>
public sealed class SqliteLocalStore : ILocalStore, IDisposable
{
    public const int RecentLookupLimit = 20;

    /// <summary>Each entry upgrades the schema by one version. Append; never edit a shipped step.</summary>
    private static readonly string[] Migrations =
    [
        """
        CREATE TABLE settings (
            key TEXT PRIMARY KEY NOT NULL,
            value TEXT NOT NULL
        );
        CREATE TABLE recent_lookups (
            serial TEXT PRIMARY KEY NOT NULL,
            device_name TEXT,
            looked_up_at INTEGER NOT NULL
        );
        """,
        // M7: site walks. Times are Unix milliseconds; flags are FlagKind raw values, one per line.
        """
        CREATE TABLE site_walks (
            id TEXT PRIMARY KEY NOT NULL,
            site TEXT NOT NULL,
            area TEXT NOT NULL,
            started_at INTEGER NOT NULL,
            ended_at INTEGER
        );
        CREATE TABLE scan_records (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            walk_id TEXT NOT NULL REFERENCES site_walks (id) ON DELETE CASCADE,
            serial TEXT NOT NULL,
            scanned_at INTEGER NOT NULL,
            status TEXT NOT NULL,
            failure_message TEXT,
            device_name TEXT,
            entra_display_name TEXT,
            primary_user TEXT,
            last_sync INTEGER,
            compliance TEXT,
            trust_type TEXT,
            autopilot_group_tag TEXT,
            flags TEXT NOT NULL DEFAULT '',
            worst_severity TEXT
        );
        CREATE INDEX scan_records_walk ON scan_records (walk_id);
        """,
    ];

    private const string ScanColumns =
        "id, walk_id, serial, scanned_at, status, failure_message, device_name, entra_display_name, primary_user, last_sync, compliance, trust_type, autopilot_group_tag, flags, worst_severity";

    private readonly SqliteConnection _connection;
    private readonly Lock _lock = new();

    private SqliteLocalStore(SqliteConnection connection, bool isTemporary)
    {
        _connection = connection;
        IsTemporary = isTemporary;
        using (var foreignKeys = Command("PRAGMA foreign_keys = ON"))
        {
            foreignKeys.ExecuteNonQuery();
        }
        Migrate();
    }

    public bool IsTemporary { get; }

    /// <summary>Opens (or creates) the database file; falls back to a temporary in-memory store if it can't.</summary>
    public static SqliteLocalStore Open(string path)
    {
        SqliteConnection? connection = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            }.ToString());
            connection.Open();
            return new SqliteLocalStore(connection, isTemporary: false);
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Local store couldn't open: {error.GetType().Name}");
            connection?.Dispose();
            return OpenInMemory(isTemporary: true);
        }
    }

    /// <summary>An in-memory store, gone when disposed. Tests use it; the app only as a fallback.</summary>
    public static SqliteLocalStore OpenInMemory(bool isTemporary = false)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return new SqliteLocalStore(connection, isTemporary);
    }

    public string? GetSetting(string key)
    {
        lock (_lock)
        {
            using var command = Command("SELECT value FROM settings WHERE key = $key", ("$key", key));
            return command.ExecuteScalar() as string;
        }
    }

    public void SetSetting(string key, string value)
    {
        lock (_lock)
        {
            using var command = Command(
                "INSERT INTO settings (key, value) VALUES ($key, $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value",
                ("$key", key), ("$value", value));
            command.ExecuteNonQuery();
        }
    }

    public void RemoveSetting(string key)
    {
        lock (_lock)
        {
            using var command = Command("DELETE FROM settings WHERE key = $key", ("$key", key));
            command.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<RecentLookup> RecentLookups()
    {
        lock (_lock)
        {
            using var command = Command(
                "SELECT serial, device_name, looked_up_at FROM recent_lookups ORDER BY looked_up_at DESC, rowid DESC LIMIT $limit",
                ("$limit", RecentLookupLimit));
            using var reader = command.ExecuteReader();
            var lookups = new List<RecentLookup>();
            while (reader.Read())
            {
                lookups.Add(new RecentLookup(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2))));
            }
            return lookups;
        }
    }

    public void RecordLookup(string serial, string? deviceName, DateTimeOffset lookedUpAt)
    {
        lock (_lock)
        {
            using var transaction = _connection.BeginTransaction();
            using (var upsert = Command(
                """
                INSERT INTO recent_lookups (serial, device_name, looked_up_at) VALUES ($serial, $name, $at)
                ON CONFLICT(serial) DO UPDATE SET
                    looked_up_at = excluded.looked_up_at,
                    device_name = COALESCE(excluded.device_name, recent_lookups.device_name)
                """,
                ("$serial", serial), ("$name", deviceName), ("$at", lookedUpAt.ToUnixTimeMilliseconds())))
            {
                upsert.ExecuteNonQuery();
            }
            using (var trim = Command(
                """
                DELETE FROM recent_lookups WHERE serial NOT IN
                    (SELECT serial FROM recent_lookups ORDER BY looked_up_at DESC, rowid DESC LIMIT $limit)
                """,
                ("$limit", RecentLookupLimit)))
            {
                trim.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

    public void ClearLocalData(IReadOnlyCollection<string> keepSettings)
    {
        lock (_lock)
        {
            using var transaction = _connection.BeginTransaction();
            using (var recents = Command("DELETE FROM recent_lookups; DELETE FROM scan_records; DELETE FROM site_walks;"))
            {
                recents.ExecuteNonQuery();
            }
            var keep = keepSettings.ToList();
            var placeholders = string.Join(", ", keep.Select((_, index) => $"$keep{index}"));
            var sql = keep.Count == 0 ? "DELETE FROM settings" : $"DELETE FROM settings WHERE key NOT IN ({placeholders})";
            using (var settings = Command(sql, keep.Select((key, index) => ($"$keep{index}", (object?)key)).ToArray()))
            {
                settings.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

    public IReadOnlyList<SiteWalk> SiteWalks()
    {
        lock (_lock)
        {
            using var command = Command("SELECT id, site, area, started_at, ended_at FROM site_walks ORDER BY started_at DESC, rowid DESC");
            using var reader = command.ExecuteReader();
            var walks = new List<SiteWalk>();
            while (reader.Read()) walks.Add(ReadWalk(reader));
            return walks;
        }
    }

    public SiteWalk? SiteWalk(string id)
    {
        lock (_lock)
        {
            using var command = Command("SELECT id, site, area, started_at, ended_at FROM site_walks WHERE id = $id", ("$id", id));
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadWalk(reader) : null;
        }
    }

    public SiteWalk StartSiteWalk(string site, string area, DateTimeOffset startedAt)
    {
        var walk = new SiteWalk(Guid.NewGuid().ToString("D"), site, area, startedAt, null);
        lock (_lock)
        {
            using var command = Command(
                "INSERT INTO site_walks (id, site, area, started_at) VALUES ($id, $site, $area, $at)",
                ("$id", walk.Id), ("$site", site), ("$area", area), ("$at", startedAt.ToUnixTimeMilliseconds()));
            command.ExecuteNonQuery();
        }
        return walk;
    }

    public void EndSiteWalk(string id, DateTimeOffset endedAt)
    {
        lock (_lock)
        {
            using var command = Command("UPDATE site_walks SET ended_at = $at WHERE id = $id", ("$id", id), ("$at", endedAt.ToUnixTimeMilliseconds()));
            command.ExecuteNonQuery();
        }
    }

    public void DeleteSiteWalk(string id)
    {
        lock (_lock)
        {
            using var command = Command("DELETE FROM site_walks WHERE id = $id", ("$id", id));
            command.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<ScanRecord> Scans(string walkId)
    {
        lock (_lock)
        {
            using var command = Command($"SELECT {ScanColumns} FROM scan_records WHERE walk_id = $walk ORDER BY scanned_at, id", ("$walk", walkId));
            using var reader = command.ExecuteReader();
            var scans = new List<ScanRecord>();
            while (reader.Read()) scans.Add(ReadScan(reader));
            return scans;
        }
    }

    public ScanRecord AddScan(string walkId, string serial, DateTimeOffset scannedAt)
    {
        lock (_lock)
        {
            using var command = Command(
                "INSERT INTO scan_records (walk_id, serial, scanned_at, status) VALUES ($walk, $serial, $at, $status) RETURNING id",
                ("$walk", walkId), ("$serial", serial), ("$at", scannedAt.ToUnixTimeMilliseconds()), ("$status", nameof(ScanStatus.Pending)));
            var id = Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            return new ScanRecord(id, walkId, serial, scannedAt, ScanStatus.Pending);
        }
    }

    public void UpdateScan(ScanRecord scan)
    {
        lock (_lock)
        {
            using var command = Command(
                """
                UPDATE scan_records SET status = $status, failure_message = $failure, device_name = $name,
                    entra_display_name = $entra, primary_user = $user, last_sync = $sync, compliance = $compliance,
                    trust_type = $trust, autopilot_group_tag = $tag, flags = $flags, worst_severity = $severity
                WHERE id = $id
                """,
                ("$id", scan.Id), ("$status", scan.Status.ToString()), ("$failure", scan.FailureMessage),
                ("$name", scan.DeviceName), ("$entra", scan.EntraDisplayName), ("$user", scan.PrimaryUser),
                ("$sync", scan.LastSync?.ToUnixTimeMilliseconds()), ("$compliance", scan.Compliance),
                ("$trust", scan.TrustType), ("$tag", scan.AutopilotGroupTag),
                ("$flags", string.Join('\n', scan.FlagValues)), ("$severity", scan.WorstSeverity));
            command.ExecuteNonQuery();
        }
    }

    public void DeleteScan(long id)
    {
        lock (_lock)
        {
            using var command = Command("DELETE FROM scan_records WHERE id = $id", ("$id", id));
            command.ExecuteNonQuery();
        }
    }

    private static SiteWalk ReadWalk(SqliteDataReader reader) =>
        new(reader.GetString(0), reader.GetString(1), reader.GetString(2),
            DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3)),
            reader.IsDBNull(4) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4)));

    private static ScanRecord ReadScan(SqliteDataReader reader)
    {
        string? Text(int column) => reader.IsDBNull(column) ? null : reader.GetString(column);
        return new ScanRecord(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3)),
            Enum.TryParse<ScanStatus>(reader.GetString(4), out var status) ? status : ScanStatus.Pending,
            Text(5),
            Text(6),
            Text(7),
            Text(8),
            reader.IsDBNull(9) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9)),
            Text(10),
            Text(11),
            Text(12),
            reader.GetString(13).Split('\n', StringSplitOptions.RemoveEmptyEntries),
            Text(14));
    }

    public void Dispose() => _connection.Dispose();

    private void Migrate()
    {
        lock (_lock)
        {
            using var read = Command("PRAGMA user_version");
            var version = Convert.ToInt32(read.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            for (var step = version; step < Migrations.Length; step++)
            {
                using var transaction = _connection.BeginTransaction();
                using (var migrate = Command(Migrations[step]))
                {
                    migrate.ExecuteNonQuery();
                }
                using (var bump = Command($"PRAGMA user_version = {step + 1}"))
                {
                    bump.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }
    }

    private SqliteCommand Command(string sql, params (string Name, object? Value)[] parameters)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        return command;
    }
}
