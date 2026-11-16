using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal static class SideWidgetLauncher
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string appDirectory = Path.Combine(root, "App");
            if (!File.Exists(Path.Combine(appDirectory, "SideWidget.App.exe")))
                return ShowError("App\\SideWidget.App.exe is missing. Extract the entire SideWidget ZIP and try again.");

            string dotnet = null;
            var catalog = Assembly.GetExecutingAssembly().GetManifestResourceStream("SideWidgetDependencies");
            if (catalog != null)
            {
                var dependencies = new SideWidgetPrerequisites(root, catalog);
                bool verifyLocal = args.Length == 2 && args[0] == "--verify-portable" &&
                    Environment.GetEnvironmentVariable("SIDEWIDGET_VERIFY_LOCAL_DEPENDENCIES") == "1";
                dependencies.Prepare(verifyLocal);
                appDirectory = dependencies.AppDirectory;
                dotnet = dependencies.DotnetRoot;
            }
            if (args.Length == 1 && args[0] == "--install-dependencies") return 0;

            var start = new ProcessStartInfo(Path.Combine(appDirectory, "SideWidget.App.exe"))
            {
                Arguments = string.Join(" ", Array.ConvertAll(args, QuoteArgument)),
                WorkingDirectory = appDirectory,
                UseShellExecute = false
            };
            if (dotnet != null)
            {
                start.EnvironmentVariables["DOTNET_ROOT_X64"] = dotnet;
                start.EnvironmentVariables["DOTNET_ROOT"] = dotnet;
            }
            using (Process app = StartApp(start))
            {
                if (app == null)
                {
                    return ShowError("Windows could not start SideWidget.");
                }

                if ((args.Length == 2 && (args[0] == "--verify-portable" || args[0] == "--share-portable")) ||
                    (args.Length > 0 && args[0] == "--phone-network-setup"))
                {
                    app.WaitForExit();
                    return app.ExitCode;
                }
            }

            return 0;
        }
        catch (OperationCanceledException) { return 1; }
        catch (WebException error) { return ShowError("Could not download dependencies from GitHub. " + error.Message); }
        catch (IOException error) { return ShowError("Could not prepare SideWidget. " + error.Message); }
        catch (InvalidDataException error) { return ShowError("Could not prepare SideWidget. " + error.Message); }
        catch (UnauthorizedAccessException error) { return ShowError("Could not write dependencies. " + error.Message); }
        catch (COMException error) { return ShowError("Could not prepare SideWidget. " + error.Message); }
        catch (Win32Exception error)
        {
            return ShowError(error.NativeErrorCode == 1223
                ? "SideWidget could not start because the Windows elevation prompt was cancelled."
                : "SideWidget could not start. " + error.Message);
        }
    }

    private static Process StartApp(ProcessStartInfo start)
    {
        try
        {
            return Process.Start(start);
        }
        catch (Win32Exception error)
        {
            if (error.NativeErrorCode != 740)
            {
                throw;
            }

            start.UseShellExecute = true;
            start.Verb = "runas";
            return Process.Start(start);
        }
    }

    private static string QuoteArgument(string argument)
    {
        // Windows treats backslashes before a quote differently from ordinary backslashes.
        var quoted = new StringBuilder("\"");
        int backslashes = 0;
        foreach (char character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                quoted.Append('\\', backslashes * 2 + 1);
            }
            else
            {
                quoted.Append('\\', backslashes);
            }

            quoted.Append(character);
            backslashes = 0;
        }

        quoted.Append('\\', backslashes * 2);
        return quoted.Append('"').ToString();
    }

    private static int ShowError(string message)
    {
        MessageBox.Show(message, "SideWidget", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return 1;
    }
}
