using System.Text.Json;
using Microsoft.Data.Sqlite;
using SideWidget.Core;

namespace SideWidget.Infrastructure;
public sealed class Store
{
    readonly string connectionString;
    public string DirectoryPath { get; }
    public bool Recovered { get; private set; }
    public Store(string directory)
    {
        DirectoryPath=directory; Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"sidewidget.db");
        connectionString=new SqliteConnectionStringBuilder {DataSource=path,Pooling=false}.ToString();
        var hasSettings=false;
        if(File.Exists(path))
        {
            try { hasSettings=InspectDatabase(path,checkVersion:true); }
            catch(SqliteException ex) when(ex.SqliteErrorCode is 11 or 26)
            {
                // A damaged WAL database cannot be safely recovered by moving only
                // its main file. Preserve the entire family for explicit recovery.
                if(File.Exists(path+"-wal")||File.Exists(path+"-shm")||File.Exists(path+"-journal"))
                    throw new CommandFault("storage","База настроек повреждена. Файлы базы и журнала сохранены; требуется восстановление.");
                File.Move(path,path+".corrupt-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N"));
                Recovered=true;
            }
        }
        if(File.Exists(path))EnsureBackup(path,hasSettings);
        Initialize();
    }
    static SqliteConnection ReadOnly(string path)=>new(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());
    static bool InspectDatabase(string path,bool checkVersion=false)
    {
        using var connection=ReadOnly(path);connection.Open();using var command=connection.CreateCommand();
        command.CommandText="PRAGMA user_version";var version=Convert.ToInt32(command.ExecuteScalar());
        if(checkVersion&&version>2)throw new CommandFault("storage","Настройки созданы более новой версией SideWidget. Обновите приложение; база не изменена.");
        command.CommandText="PRAGMA quick_check(1)";
        if(!string.Equals(command.ExecuteScalar() as string,"ok",StringComparison.Ordinal))throw new SqliteException("Database integrity check failed",11);
        command.CommandText="SELECT count(*) FROM sqlite_master WHERE type='table' AND name='settings'";
        return Convert.ToInt32(command.ExecuteScalar())>0;
    }
    static bool ValidBackup(string path,bool requireSettings)
    {
        if(!File.Exists(path)||new FileInfo(path).Length<100)return false;
        try { var hasSettings=InspectDatabase(path);return !requireSettings||hasSettings; }
        catch(SqliteException ex) when(ex.SqliteErrorCode is 11 or 26){return false;}
    }
    void EnsureBackup(string path,bool requireSettings)
    {
        var backupDirectory=Path.Combine(DirectoryPath,"backups");var backup=Path.Combine(backupDirectory,"before-product-reset-v2.db");
        if(ValidBackup(backup,requireSettings))return;
        Directory.CreateDirectory(backupDirectory);
        var temporary=Path.Combine(backupDirectory,"before-product-reset-v2."+Guid.NewGuid().ToString("N")+".partial");
        // SQLite backup includes committed WAL data. A failed copy keeps its
        // partial artifact and never publishes the final name or migrates source.
        using(var source=ReadOnly(path))
        using(var target=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=temporary,Pooling=false}.ToString()))
        {source.Open();target.Open();source.BackupDatabase(target);}
        if(!ValidBackup(temporary,requireSettings))throw new CommandFault("storage","Не удалось проверить резервную копию настроек. Исходная база не изменена.");
        using(var file=new FileStream(temporary,FileMode.Open,FileAccess.ReadWrite,FileShare.Read))file.Flush(flushToDisk:true);
        if(File.Exists(backup))File.Move(backup,backup+".invalid-"+Guid.NewGuid().ToString("N"));
        File.Move(temporary,backup);
    }
    SqliteConnection Open() { var c=new SqliteConnection(connectionString); c.Open(); using var quota=c.CreateCommand(); quota.CommandText="PRAGMA max_page_count=4096"; quota.ExecuteNonQuery(); return c; }
    void Initialize()
    {
        using var c=Open(); using var cmd=c.CreateCommand();
        cmd.CommandText="PRAGMA journal_mode=DELETE; PRAGMA max_page_count=4096; CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY,value TEXT NOT NULL); CREATE TABLE IF NOT EXISTS journal(id INTEGER PRIMARY KEY,time TEXT,action TEXT,status TEXT,duration INTEGER); PRAGMA user_version=2;"; cmd.ExecuteNonQuery();
    }
    public T Read<T>(string key,T fallback)
    {
        using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT value FROM settings WHERE key=$key"; cmd.Parameters.AddWithValue("$key",key);
        try { return cmd.ExecuteScalar() is string value ? JsonSerializer.Deserialize<T>(value,Protocol.Json) ?? fallback : fallback; }
        catch(JsonException) { Recovered=true; return fallback; }
    }
    public void Save<T>(string key,T value)
    {
        var json=JsonSerializer.Serialize(value,Protocol.Json); if(json.Length>256*1024) throw new CommandFault("limit","Настройки слишком большие");
        using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT INTO settings VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=$value"; cmd.Parameters.AddWithValue("$key",key); cmd.Parameters.AddWithValue("$value",json); cmd.ExecuteNonQuery();
    }
    const string AppIconPrefix = "personal.appIcon.v2.";
    public List<PinnedApp> ReadApps()
    {
        var apps = Read("personal.apps", new List<PinnedApp>());
        return apps.Select(app => app.Icon is null
            ? app with { Icon = Read<string?>(AppIconPrefix + app.Id, null) }
            : app).ToList();
    }
    public void SaveApps(IReadOnlyList<PinnedApp> apps)
    {
        if (apps.Count > 40) throw new CommandFault("limit", "Можно закрепить до 40 приложений");
        if (apps.Select(app => app.Id).Distinct(StringComparer.Ordinal).Count() != apps.Count ||
            apps.Any(app => !System.Text.RegularExpressions.Regex.IsMatch(app.Id, "^[a-zA-Z0-9-]{1,64}$")))
            throw new CommandFault("application", "Некорректный идентификатор приложения");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var app in apps)
        {
            if (app.Icon is null) continue;
            var icon = JsonSerializer.Serialize(app.Icon, Protocol.Json);
            if (icon.Length > 220 * 1024) throw new CommandFault("icon", "Значок слишком большой. Выберите изображение меньшего размера.");
            values.Add(AppIconPrefix + app.Id, icon);
        }
        var metadata = JsonSerializer.Serialize(apps.Select(app => app with { Icon = null }), Protocol.Json);
        if (metadata.Length > 256 * 1024) throw new CommandFault("limit", "Список приложений слишком большой");
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var orphaned = new List<string>();
        using (var query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = "SELECT key FROM settings WHERE substr(key,1,$length)=$prefix";
            query.Parameters.AddWithValue("$length", AppIconPrefix.Length);
            query.Parameters.AddWithValue("$prefix", AppIconPrefix);
            using var reader = query.ExecuteReader();
            while (reader.Read()) if (!values.ContainsKey(reader.GetString(0))) orphaned.Add(reader.GetString(0));
        }
        foreach (var key in orphaned)
        {
            using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM settings WHERE key=$key";
            delete.Parameters.AddWithValue("$key", key);
            delete.ExecuteNonQuery();
        }
        values.Add("personal.apps", metadata);
        foreach (var pair in values)
        {
            using var write = connection.CreateCommand();
            write.Transaction = transaction;
            write.CommandText = "INSERT INTO settings VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=$value";
            write.Parameters.AddWithValue("$key", pair.Key);
            write.Parameters.AddWithValue("$value", pair.Value);
            write.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    public void Journal(string action,string status,long duration)
    {
        using var c=Open(); using var cmd=c.CreateCommand();
        cmd.CommandText="INSERT INTO journal(time,action,status,duration) VALUES($time,$action,$status,$duration); DELETE FROM journal WHERE time < $cutoff OR id NOT IN(SELECT id FROM journal ORDER BY id DESC LIMIT 1000);";
        cmd.Parameters.AddWithValue("$time",DateTimeOffset.UtcNow.ToString("O")); cmd.Parameters.AddWithValue("$action",action); cmd.Parameters.AddWithValue("$status",status); cmd.Parameters.AddWithValue("$duration",duration); cmd.Parameters.AddWithValue("$cutoff",DateTimeOffset.UtcNow.AddDays(-30).ToString("O")); cmd.ExecuteNonQuery();
    }
    public object[] History()
    {
        using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT time,action,status,duration FROM journal ORDER BY id DESC LIMIT 50"; using var r=cmd.ExecuteReader(); var result=new List<object>();
        while(r.Read()) result.Add(new {time=r.GetString(0),action=r.GetString(1),status=r.GetString(2),duration=r.GetInt64(3)}); return result.ToArray();
    }
}


