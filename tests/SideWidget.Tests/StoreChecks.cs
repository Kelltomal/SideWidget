using System.Text;
using Microsoft.Data.Sqlite;
using SideWidget.Core;
using SideWidget.Infrastructure;

public static class StoreChecks
{
    public static int Run()
    {
        var count = 0;
        var failures = new List<Exception>();
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            count++;
        }
        void Scenario(string name, Action test)
        {
            try { test(); }
            catch (Exception error) { failures.Add(new Exception(name, error)); }
        }

        Scenario("corrupt database preserves original bytes and recovers", () => {
            using var directory = new TemporaryDirectory();
            var original = Encoding.UTF8.GetBytes("synthetic damaged sqlite file; preserve these bytes");
            File.WriteAllBytes(directory.Database, original);
            var store = new Store(directory.Path);
            Check(store.Recovered, "corruption is reported");
            var archived = Directory.GetFiles(directory.Path, "sidewidget.db.corrupt-*");
            Check(archived.Length == 1 && File.ReadAllBytes(archived[0]).SequenceEqual(original), "damaged database is retained intact");
            store.Save("local-draft", "new local draft");
            Check(store.Read("local-draft", "") == "new local draft", "recovered store is writable");
        });

        Scenario("invalid prior backup is replaced before migration without losing its bytes", () => {
            using var directory = new TemporaryDirectory();
            CreateDatabase(directory.Database, 1);
            var backupDirectory = System.IO.Path.Combine(directory.Path, "backups");
            Directory.CreateDirectory(backupDirectory);
            var backup = System.IO.Path.Combine(backupDirectory, "before-product-reset-v2.db");
            var partial = Encoding.UTF8.GetBytes("synthetic interrupted backup marker");
            File.WriteAllBytes(backup, partial);
            var store = new Store(directory.Path);
            Check(ReadScalar(backup, "SELECT value FROM settings WHERE key='old-profile'") == "\"preserve-original\"", "backup contains original user settings");
            Check(ReadScalar(backup, "PRAGMA user_version") == "1", "backup retains pre-migration schema version");
            Check(Directory.GetFiles(backupDirectory).Any(path => path != backup && File.ReadAllBytes(path).SequenceEqual(partial)), "invalid prior backup remains available for inspection");
            store.Save("old-profile", "new-value");
            _ = new Store(directory.Path);
            Check(ReadScalar(backup, "SELECT value FROM settings WHERE key='old-profile'") == "\"preserve-original\"", "later launches do not overwrite valid pre-migration backup");
        });

        Scenario("newer database version fails closed", () => {
            using var directory = new TemporaryDirectory();
            CreateDatabase(directory.Database, 3);
            var rejected = false;
            try { _ = new Store(directory.Path); }
            catch (CommandFault error) when (error.Code == "storage") { rejected = true; }
            Check(rejected, "older app rejects future schema");
            Check(ReadScalar(directory.Database, "PRAGMA user_version") == "3", "future schema version is never downgraded");
            Check(ReadScalar(directory.Database, "SELECT value FROM settings WHERE key='old-profile'") == "\"preserve-original\"", "future database data is unchanged");
        });

        Scenario("corrupt database with WAL fails closed without discarding transaction data", () => {
            using var directory = new TemporaryDirectory();
            var original = Encoding.UTF8.GetBytes("synthetic corrupt main database");
            var wal = Encoding.UTF8.GetBytes("synthetic WAL potentially containing recoverable data");
            File.WriteAllBytes(directory.Database, original);
            File.WriteAllBytes(directory.Database + "-wal", wal);
            var rejected = false;
            try { _ = new Store(directory.Path); }
            catch (CommandFault error) when (error.Code == "storage") { rejected = true; }
            Check(rejected, "corrupt database with sidecars requires explicit recovery");
            Check(File.ReadAllBytes(directory.Database).SequenceEqual(original), "main database is preserved");
            Check(File.ReadAllBytes(directory.Database + "-wal").SequenceEqual(wal), "WAL bytes are preserved");
        });

        if (failures.Count != 0)
        {
            Console.WriteLine($"Store safety: {count} checks passed, {failures.Count} scenarios failed.");
            throw new AggregateException("Store safety checks failed", failures);
        }
        Console.WriteLine($"PASS {count} store backup/recovery checks.");
        return count;
    }

    static void CreateDatabase(string path, int version)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder {DataSource = path, Pooling = false}.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT NOT NULL); INSERT INTO settings VALUES('old-profile','\"preserve-original\"'); PRAGMA user_version=" + version;
        command.ExecuteNonQuery();
    }

    static string ReadScalar(string path, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder {DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false}.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SideWidget-store-check-" + Guid.NewGuid().ToString("N"));
        public string Database => System.IO.Path.Combine(Path, "sidewidget.db");
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            var resolved = System.IO.Path.GetFullPath(Path);
            var root = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.Path.GetFileName(resolved).StartsWith("SideWidget-store-check-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside test directory");
            Directory.Delete(resolved, recursive: true);
        }
    }
}
