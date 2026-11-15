using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

internal static class SideWidgetLauncher
{
    [STAThread]
    private static int Main(string[] args)
    {
        string appDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App");
        string executable = Path.Combine(appDirectory, "SideWidget.App.exe");
        if (!File.Exists(executable))
        {
            return ShowError("App\\SideWidget.App.exe is missing. Extract the entire SideWidget ZIP and try again.");
        }

        var start = new ProcessStartInfo(executable)
        {
            Arguments = string.Join(" ", Array.ConvertAll(args, QuoteArgument)),
            WorkingDirectory = appDirectory,
            UseShellExecute = false
        };

        try
        {
            using (Process app = StartApp(start))
            {
                if (app == null)
                {
                    return ShowError("Windows could not start SideWidget.");
                }

                if (args.Length == 2 && (args[0] == "--verify-portable" || args[0] == "--share-portable"))
                {
                    app.WaitForExit();
                    return app.ExitCode;
                }
            }

            return 0;
        }
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
