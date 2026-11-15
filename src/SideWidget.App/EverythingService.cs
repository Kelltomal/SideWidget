using System.Runtime.InteropServices;
using System.Text;

namespace SideWidget.App;

public sealed class EverythingService
{
    // The SDK owns one global query/result state. Never interleave queries, even across service instances.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<object> Query(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > 1024 || text.Contains('\0')) throw new ArgumentException("Некорректный поисковый запрос.", nameof(text));
        if (!await Gate.WaitAsync(TimeSpan.FromSeconds(5))) return Unavailable("Everything занят предыдущим запросом.");
        var work = Task.Run(() =>
        {
            try
            {
                if (FindWindowW("EVERYTHING_TASKBAR_NOTIFICATION", null) == IntPtr.Zero)
                    return Unavailable("Everything недоступен: запустите обычный экземпляр в текущем сеансе Windows. Именованные экземпляры не поддерживаются.");
                Everything_Reset();
                Everything_SetSearchW(text);
                Everything_SetMax(50);
                Everything_SetRequestFlags(3); // File name + path; supported by both IPC versions.
                if (!Everything_QueryW(true))
                    return Unavailable($"Ошибка IPC Everything ({Everything_GetLastError()}). Проверьте запущенный экземпляр и права доступа.");
                var results = new List<object>();
                if (!string.IsNullOrWhiteSpace(text))
                    for (uint i = 0, count = Math.Min(Everything_GetNumResults(), 50); i < count; i++)
                    {
                        var length = Everything_GetResultFullPathNameW(i, null, 0);
                        if (length == 0 || length > 32767) continue;
                        var buffer = new StringBuilder((int)length + 1);
                        Everything_GetResultFullPathNameW(i, buffer, (uint)buffer.Capacity);
                        var path = buffer.ToString();
                        results.Add(new { path, name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)), isDirectory = Everything_IsFolderResult(i) });
                    }
                return (object)new { available = true, results };
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            { return Unavailable("Библиотека Everything SDK недоступна или несовместима с приложением."); }
            finally { Gate.Release(); }
        });
        try { return await work.WaitAsync(TimeSpan.FromSeconds(8)); }
        catch (TimeoutException) { return Unavailable("Everything не ответил вовремя. Запрос продолжает ожидание в фоне."); }
    }

    private static object Unavailable(string message) => new { available = false, results = Array.Empty<object>(), message };
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowW(string className, string? windowName);
    [DllImport("Everything64.dll")] private static extern void Everything_Reset();
    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)] private static extern void Everything_SetSearchW(string search);
    [DllImport("Everything64.dll")] private static extern void Everything_SetMax(uint max);
    [DllImport("Everything64.dll")] private static extern void Everything_SetRequestFlags(uint flags);
    [DllImport("Everything64.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Everything_QueryW([MarshalAs(UnmanagedType.Bool)] bool wait);
    [DllImport("Everything64.dll")] private static extern uint Everything_GetLastError();
    [DllImport("Everything64.dll")] private static extern uint Everything_GetNumResults();
    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)] private static extern uint Everything_GetResultFullPathNameW(uint index, StringBuilder? buffer, uint capacity);
    [DllImport("Everything64.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Everything_IsFolderResult(uint index);
}
