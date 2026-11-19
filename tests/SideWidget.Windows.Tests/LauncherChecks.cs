using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using SideWidget.App;
using SideWidget.Core;
using SideWidget.Infrastructure;

internal static class LauncherChecks
{
    internal static async Task Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SideWidget-launcher-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var executable = Path.Combine(directory, "Fixture.exe");
            var shortcut = Path.Combine(directory, "Fixture shortcut.lnk");
            File.WriteAllBytes(executable, []); // Never launched: identity fixture only.
            WriteShortcut(shortcut, executable, "--profile preserved");
            var state = new PersonalState(Path.Combine(directory, "data"));
            var service = new LauncherService(state);
            var iconExecutable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            var iconShortcut = Path.Combine(directory, "Explorer.lnk");
            WriteShortcut(iconShortcut, iconExecutable, "--profile preserved");
            await service.Add(iconExecutable);
            var expectedIcon = state.Apps.Single().Icon;
            Require(expectedIcon?.StartsWith("data:image/", StringComparison.Ordinal) == true, "Executable supplies an image");
            service.Remove(state.Apps.Single().Id);

            await service.Add(iconShortcut);
            var pinnedShortcut = state.Apps.Single();
            Require(pinnedShortcut.Icon == expectedIcon && pinnedShortcut.Path == iconShortcut, "Shortcut uses the target executable icon and keeps its launch path");
            state.SaveApps([pinnedShortcut with { Icon = null, Name = "Renamed Explorer" }]);
            await service.Add(iconShortcut);
            Require(state.Apps.Single() == pinnedShortcut with { Name = "Renamed Explorer" }, "Re-adding a shortcut fills its missing icon and preserves its record");
            service.Remove(pinnedShortcut.Id);

            await service.Add(shortcut, "data:image/png;base64,AA==");
            Require(state.Apps.Single().Path == shortcut, "Shortcut remains launch target");
            Require(state.Apps.Single().Identity == executable, "Shortcut records ordinary executable identity");
            Require(!service.CanUndo, "Adding a shortcut does not invent a window selection undo");
            service.CaptureUndo();
            Require(!service.CanUndo, "Capturing unchanged list cannot expose an undo button");
            var before = state.Apps.ToArray();
            var linkBytes = File.ReadAllBytes(shortcut);
            await service.ToggleWithUndo(executable);
            Require(state.Apps.Count == 0, "Selecting executable removes its pinned shortcut instead of adding duplicate");
            Require(service.CanUndo, "Successful removal can be undone");
            ExecuteSql(state, "CREATE TRIGGER reject_launcher BEFORE UPDATE ON settings WHEN NEW.key='personal.apps' BEGIN SELECT RAISE(ABORT,'synthetic save failure'); END;");
            var rejected = false;
            try { service.TogglePackagedWithUndo("Fixture.App_123!App", "Packaged fixture"); }
            catch (SqliteException) { rejected = true; }
            Require(rejected && state.Apps.Count == 0 && service.CanUndo, "Save failure preserves current state and earlier undo");
            ExecuteSql(state, "DROP TRIGGER reject_launcher;");
            service.Undo();
            Require(!service.CanUndo && state.Apps.SequenceEqual(before), "Undo restores original shortcut, icon and executable identity");
            Require(File.ReadAllBytes(shortcut).SequenceEqual(linkBytes), "Pinning and toggling never modify shortcut arguments or working directory");

            service.SetIcon(before[0].Id, "data:image/png;base64,AQ==");
            Require(state.Apps[0].Path == shortcut && state.Apps[0].Identity == executable, "Changing icon preserves launch target and identity");
            Require(new PersonalState(state.Store.DirectoryPath).Apps[0].Icon == "data:image/png;base64,AQ==", "Changed icon persists");
            // Old inline application records have no Identity field. Resolve their actual .lnk on demand.
            state.SaveApps(state.Apps.Select(app => app with { Identity = null }));
            await service.ToggleWithUndo(executable);
            Require(state.Apps.Count == 0, "Legacy shortcut without saved identity still matches selected executable");
            service.Undo();
            service.TogglePackagedWithUndo("Fixture.App_123!App", "Packaged fixture");
            Require(state.Apps.Count == 2 && service.CanUndo, "Explicit packaged identity remains separate from executable identity");
            service.Undo();
            Require(state.Apps.Count == 1 && !service.CanUndo, "Packaged toggle undo restores prior list");

            state.SaveApps(Enumerable.Range(0, 39).Select(index => new PinnedApp("fixture-" + index, "Fixture", "aumid:Fixture_" + index + "!App", Packaged: true)));
            var beforeLimit = state.Apps.ToArray();
            service.TogglePackagedWithUndo("Fixture_39!App", "Fortieth");
            rejected = false;
            try { service.TogglePackagedWithUndo("Fixture_40!App", "Forty-first"); }
            catch (CommandFault error) when (error.Code == "limit") { rejected = true; }
            Require(rejected && state.Apps.Count == 40 && service.CanUndo, "App limit failure preserves earlier undo");
            service.Undo();
            Require(state.Apps.SequenceEqual(beforeLimit), "Undo after failed toggle still restores last successful change");
        }
        finally
        {
            var resolved = Path.GetFullPath(directory);
            var root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("SideWidget-launcher-native-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside test directory");
            Directory.Delete(resolved, true);
        }
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void ExecuteSql(PersonalState state, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(state.Store.DirectoryPath, "sidewidget.db"), Pooling = false }.ToString());
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
    static void WriteShortcut(string path, string target, string arguments)
    {
        object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            dynamic link = ((dynamic)shell!).CreateShortcut(path); shortcut = link;
            link.TargetPath = target; link.Arguments = arguments; link.Save();
        }
        finally
        {
            if (shortcut is not null) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null) Marshal.FinalReleaseComObject(shell);
        }
    }
}
