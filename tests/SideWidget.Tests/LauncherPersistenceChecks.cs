using System.Text.Json;
using Microsoft.Data.Sqlite;
using SideWidget.Core;
using SideWidget.Infrastructure;

public static class LauncherPersistenceChecks
{
    public static int Run()
    {
        var count = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; }
        var directory = Path.Combine(Path.GetTempPath(), "SideWidget-launcher-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var state = new PersonalState(directory);
            var icon = "data:image/png;base64," + Convert.ToBase64String(new byte[100000]);
            state.SaveApps([new("one", "First", "C:\\First.exe", icon), new("two", "Second", "C:\\Second.exe", icon)]);
            var restored = new PersonalState(directory);
            Check(restored.Apps.Count == 2 && restored.Apps.All(app => app.Icon == icon), "Two allowed 100 KB icons persist and reload intact");
            Check(state.Store.Read<JsonElement>("personal.apps", default).EnumerateArray().All(app => app.GetProperty("icon").ValueKind == JsonValueKind.Null), "Metadata does not duplicate image payloads");

            var original = state.Apps.ToArray();
            var rejected = false;
            try { state.SaveApps([original[0] with { Name = "Must not be saved" }, original[1] with { Icon = new string('x', 221 * 1024) }]); }
            catch (CommandFault error) when (error.Code == "icon") { rejected = true; }
            Check(rejected && state.Apps.SequenceEqual(original), "Oversized icon rejects entire candidate without changing memory");
            Check(new PersonalState(directory).Apps.SequenceEqual(original), "Oversized candidate leaves persisted apps and icons unchanged");

            var legacy = new PersonalState(Path.Combine(directory, "legacy"));
            legacy.Store.Save("personal.apps", new[] { new PinnedApp("legacy", "Old app", "C:\\Old.exe", icon) });
            legacy.Store.Save("profiles", "preserve-legacy-settings");
            legacy.Store.Save("personal.appIcon.custom", "unrelated-key");
            legacy = new PersonalState(legacy.Store.DirectoryPath);
            Check(legacy.Apps.Single().Icon == icon, "Legacy inline icon still loads");
            legacy.SaveApps(legacy.Apps);
            Check(new PersonalState(legacy.Store.DirectoryPath).Apps.Single().Icon == icon, "Next save migrates inline icon without losing it");
            legacy.SaveApps([]);
            Check(legacy.Store.Read<string?>("personal.appIcon.v2.legacy", null) is null, "Removing app deletes its managed icon");
            Check(legacy.Store.Read("profiles", "") == "preserve-legacy-settings" && legacy.Store.Read("personal.appIcon.custom", "") == "unrelated-key", "Unrelated and legacy settings survive icon cleanup");

            var maximum = new PersonalState(Path.Combine(directory, "maximum"));
            var largeIcon = "data:image/png;base64," + Convert.ToBase64String(new byte[150000]);
            maximum.SaveApps(Enumerable.Range(0, 40).Select(index => new PinnedApp("app-" + index, "App " + index, "C:\\App" + index + ".exe", largeIcon)));
            Check(new PersonalState(maximum.Store.DirectoryPath).Apps.Count == 40, "Forty maximum size custom icons fit within database quota");
            rejected = false;
            try { maximum.SaveApps(maximum.Apps.Append(new("overflow", "Overflow", "C:\\Overflow.exe"))); }
            catch (CommandFault error) when (error.Code == "limit") { rejected = true; }
            Check(rejected && maximum.Apps.Count == 40 && new PersonalState(maximum.Store.DirectoryPath).Apps.Count == 40, "Forty-first app cannot mutate memory or database");

            // Fill the same real database until SQLite enforces its configured quota.
            var full = false;
            try { for (var index = 0; index < 150; index++) state.Store.Save("filler-" + index, new string('x', 128 * 1024)); }
            catch (SqliteException error) when (error.SqliteErrorCode == 13) { full = true; }
            Check(full, "Quota failure fixture actually reaches SQLITE_FULL");
            rejected = false;
            try { state.SaveApps([original[1] with { Name = "Must roll back" }, new("three", "Third", "C:\\Third.exe", largeIcon), new("four", "Fourth", "C:\\Fourth.exe", largeIcon)]); }
            catch (SqliteException error) when (error.SqliteErrorCode == 13) { rejected = true; }
            Check(rejected && state.Apps.SequenceEqual(original), "Failed quota write keeps current in-memory launcher");
            Check(new PersonalState(directory).Apps.SequenceEqual(original), "Quota failure rolls back icon deletions, new icons and metadata together");
            Check(state.Store.Read<string?>("personal.appIcon.v2.three", null) is null && state.Store.Read<string?>("personal.appIcon.v2.four", null) is null, "Quota rollback leaves no partial new icons");
            Console.WriteLine($"PASS {count} launcher persistence checks.");
            return count;
        }
        finally
        {
            var resolved = Path.GetFullPath(directory);
            var root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("SideWidget-launcher-check-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside test directory");
            Directory.Delete(resolved, true);
        }
    }
}
