using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace SideWidget.App;

public sealed record ScreenPick(string? Hex, string? ExecutablePath, string? AppUserModelId, string? Name);

/// <summary>A bounded, explicit user selection. No screen capture is retained.</summary>
public static class ScreenPicker
{
    private static int active;

    public static Task<ScreenPick> PickAsync(nint owner, bool color)
    {
        if (owner == 0 || !IsWindow(owner)) return Task.FromException<ScreenPick>(new ArgumentException("Окно SideWidget закрыто."));
        if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
            return Task.FromException<ScreenPick>(new InvalidOperationException("Выбор уже открыт."));
        var completion = new TaskCompletionSource<ScreenPick>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var thread = new Thread(() => new Selection(owner, color, completion).Run()) { IsBackground = true, Name = "SideWidget screen picker" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        catch (Exception e) { Interlocked.Exchange(ref active, 0); completion.TrySetException(e); }
        return completion.Task;
    }

    private sealed class Selection(nint owner, bool color, TaskCompletionSource<ScreenPick> completion)
    {
        private const uint MoveMessage = 0x8001, PickMessage = 0x8002, CancelMessage = 0x8003;
        private const uint TransparentColor = 0x00FF00FF;
        private readonly string className = "SideWidget.Picker." + Guid.NewGuid().ToString("N");
        private readonly nint[] borders = new nint[4];
        private readonly Dictionary<nint, HoverIdentity> hoverIdentities = new();
        private nint window, mouseHook, keyboardHook, module, priorDpi;
        private WindowProc? windowProc;
        private HookProc? mouseProc, keyboardProc;
        private Point cursor, picked;
        private int crossX = 12, crossY = 12;
        private bool leftDown, rightDown, escapeDown, moveQueued, finished;
        private ScreenPick? result;
        private Exception? error;
        private long start, lastHover;
        private nint highlightedRoot;
        private Rect highlightedBounds;
        private bool hasHighlight;
        private string hoverLabel = "Наведите на окно";
        private const int SampleSize = 15, Zoom = 9, LensSize = SampleSize * Zoom;
        private nint sampleDc, sampleBitmap, priorSampleBitmap;
        private uint previewPixel = uint.MaxValue;
        private Rect overlayBounds;
        private bool overlayVisible;
        private int OverlayHeight => color ? 164 : 116;

        internal void Run()
        {
            ushort atom = 0;
            try
            {
                priorDpi = SetThreadDpiAwarenessContext(new nint(-4));
                module = GetModuleHandleW(null);
                windowProc = WndProc;
                mouseProc = Mouse;
                keyboardProc = Keyboard;
                var definition = new WindowClass
                {
                    Size = (uint)Marshal.SizeOf<WindowClass>(), Instance = module, ClassName = className,
                    Procedure = Marshal.GetFunctionPointerForDelegate(windowProc), Cursor = LoadCursorW(0, new nint(32515))
                };
                atom = RegisterClassExW(ref definition);
                if (atom == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                // Transparent to hit testing, never activated, absent from taskbar/Alt+Tab.
                window = CreateWindowExW(0x00080000 | 0x00000020 | 0x00000080 | 0x08000000, className, "SideWidget: выбор", 0x80000000,
                    0, 0, 320, 92, 0, 0, module, 0);
                if (window == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!SetLayeredWindowAttributes(window, color ? 0 : TransparentColor, 255, color ? 2u : 1u)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (color)
                {
                    var dc = GetDC(0);
                    if (dc == 0) throw new Win32Exception("Не удалось прочитать экран.");
                    try
                    {
                        sampleDc = CreateCompatibleDC(dc);
                        sampleBitmap = CreateCompatibleBitmap(dc, SampleSize, SampleSize);
                        if (sampleDc == 0 || sampleBitmap == 0) throw new Win32Exception("Не удалось создать лупу.");
                        priorSampleBitmap = SelectObject(sampleDc, sampleBitmap);
                    }
                    finally { ReleaseDC(0, dc); }
                }
                if (!color)
                    for (var index = 0; index < borders.Length; index++)
                    {
                        // Four thin surfaces avoid allocating a full-screen transparent backing bitmap.
                        borders[index] = CreateWindowExW(0x00080000 | 0x00000020 | 0x00000080 | 0x08000000,
                            className, "SideWidget: контур выбора", 0x80000000, 0, 0, 1, 1, 0, 0, module, 0);
                        if (borders[index] == 0 || !SetLayeredWindowAttributes(borders[index], 0, 255, 2))
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                if (!GetCursorPos(out cursor)) throw new Win32Exception(Marshal.GetLastWin32Error());
                MoveOverlay();
                mouseHook = SetWindowsHookExW(14, mouseProc, module, 0);
                if (mouseHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                keyboardHook = SetWindowsHookExW(13, keyboardProc, module, 0);
                if (keyboardHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                start = Environment.TickCount64;
                if (SetTimer(window, 1, color ? 33u : 100u, 0) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                while (!finished)
                {
                    var status = GetMessageW(out var message, 0, 0, 0);
                    if (status == -1) throw new Win32Exception(Marshal.GetLastWin32Error());
                    if (status == 0) break;
                    TranslateMessage(ref message);
                    DispatchMessageW(ref message);
                }
            }
            catch (Exception e) { error = e; }
            finally
            {
                // Cleanup precedes task completion, so caller can safely open its confirmation dialog.
                if (mouseHook != 0) UnhookWindowsHookEx(mouseHook);
                if (keyboardHook != 0) UnhookWindowsHookEx(keyboardHook);
                foreach (var border in borders) if (border != 0) DestroyWindow(border);
                if (window != 0) { KillTimer(window, 1); DestroyWindow(window); }
                if (sampleDc != 0 && priorSampleBitmap != 0) SelectObject(sampleDc, priorSampleBitmap);
                if (sampleBitmap != 0) DeleteObject(sampleBitmap);
                if (sampleDc != 0) DeleteDC(sampleDc);
                if (atom != 0) UnregisterClassW(className, module);
                if (priorDpi != 0) SetThreadDpiAwarenessContext(priorDpi);
                GC.KeepAlive(windowProc); GC.KeepAlive(mouseProc); GC.KeepAlive(keyboardProc);
                Interlocked.Exchange(ref active, 0);
                if (error is OperationCanceledException) completion.TrySetCanceled();
                else if (error is not null) completion.TrySetException(error);
                else if (result is not null) completion.TrySetResult(result);
                else completion.TrySetCanceled();
            }
        }

        private nint Mouse(int code, nint message, nint data)
        {
            if (code < 0 || finished) return CallNextHookEx(mouseHook, code, message, data);
            try
            {
                var info = Marshal.PtrToStructure<MouseInfo>(data);
                switch ((uint)message)
                {
                    case 0x0200:
                        cursor = info.Position;
                        if (!moveQueued) { moveQueued = true; PostMessageW(window, MoveMessage, 0, 0); }
                        break;
                    case 0x0201: leftDown = true; picked = info.Position; return 1;
                    case 0x0202 when leftDown:
                        leftDown = false; PostMessageW(window, PickMessage, 0, 0); return 1;
                    case 0x0204: rightDown = true; return 1;
                    case 0x0205 when rightDown:
                        rightDown = false; PostMessageW(window, CancelMessage, 0, 0); return 1;
                }
            }
            catch (Exception e) { error = e; PostMessageW(window, CancelMessage, 0, 0); }
            return CallNextHookEx(mouseHook, code, message, data);
        }

        private nint Keyboard(int code, nint message, nint data)
        {
            if (code < 0 || finished) return CallNextHookEx(keyboardHook, code, message, data);
            try
            {
                var info = Marshal.PtrToStructure<KeyboardInfo>(data);
                if (info.Key == 0x1B)
                {
                    if ((uint)message is 0x0100 or 0x0104) { escapeDown = true; return 1; }
                    if (escapeDown && (uint)message is 0x0101 or 0x0105)
                    { escapeDown = false; PostMessageW(window, CancelMessage, 0, 0); return 1; }
                }
            }
            catch (Exception e) { error = e; PostMessageW(window, CancelMessage, 0, 0); }
            return CallNextHookEx(keyboardHook, code, message, data);
        }

        private nint WndProc(nint hwnd, uint message, nint wParam, nint lParam)
        {
            try
            {
                switch (message)
                {
                    case 0x000F: if (hwnd == window) Paint(hwnd); else PaintBorder(hwnd); return 0;
                    case 0x0084: return -1; // HTTRANSPARENT
                    case 0x0021: return 3; // MA_NOACTIVATE
                    case MoveMessage: moveQueued = false; if (!finished) MoveOverlay(); return 0;
                    case PickMessage:
                        if (finished) return 0;
                        ShowWindow(window, 0);
                        HideHighlight();
                        DwmFlush(); // Ensure the picker is absent from the composed pixel/target.
                        result = color ? ReadColor(picked) : ReadWindow(picked, owner);
                        finished = true;
                        return 0;
                    case CancelMessage: error ??= new OperationCanceledException("Выбор отменён."); finished = true; return 0;
                    case 0x0113:
                        if (!IsWindow(owner) || Environment.TickCount64 - start >= 30000)
                        { error = new OperationCanceledException("Время выбора истекло или окно SideWidget закрыто."); finished = true; }
                        else if (color) MoveOverlay();
                        else RefreshHover();
                        return 0;
                    case 0x0010: error = new OperationCanceledException("Выбор закрыт."); finished = true; return 0;
                }
            }
            catch (Exception e) { error = e; finished = true; return 0; }
            return DefWindowProcW(hwnd, message, wParam, lParam);
        }

        private void MoveOverlay()
        {
            // Physical pixels, including negative monitor coordinates. Keep the lens clear of its source.
            var x = cursor.X + (color ? 24 : -12);
            var y = cursor.Y + (color ? 24 : -12);
            var monitor = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfoW(MonitorFromPoint(cursor, 2), ref monitor))
            {
                if (color)
                {
                    if (x + 320 > monitor.Bounds.Right) x = cursor.X - 320 - 24;
                    if (y + OverlayHeight > monitor.Bounds.Bottom) y = cursor.Y - OverlayHeight - 24;
                }
                x = Math.Clamp(x, monitor.Bounds.Left, Math.Max(monitor.Bounds.Left, monitor.Bounds.Right - 320));
                y = Math.Clamp(y, monitor.Bounds.Top, Math.Max(monitor.Bounds.Top, monitor.Bounds.Bottom - OverlayHeight));
            }
            if (color) RefreshColor();
            crossX = cursor.X - x; crossY = cursor.Y - y;
            overlayBounds = new Rect { Left = x, Top = y, Right = x + 320, Bottom = y + OverlayHeight };
            SetWindowPos(window, new nint(-1), x, y, 320, OverlayHeight, 0x0010 | 0x0040);
            overlayVisible = true;
            RefreshHover();
            InvalidateRect(window, 0, false);
            if (color) UpdateWindow(window);
        }

        private void RefreshColor()
        {
            const int radius = SampleSize / 2;
            var source = new Rect { Left = cursor.X - radius, Top = cursor.Y - radius,
                Right = cursor.X + radius + 1, Bottom = cursor.Y + radius + 1 };
            // A fast move can put the cursor over the previous lens position. Never sample ourselves.
            if (overlayVisible && source.Left < overlayBounds.Right && source.Right > overlayBounds.Left &&
                source.Top < overlayBounds.Bottom && source.Bottom > overlayBounds.Top)
            {
                ShowWindow(window, 0);
                overlayVisible = false;
                DwmFlush();
            }
            var background = CreateSolidBrush(0x001C1A18);
            var area = new Rect { Right = SampleSize, Bottom = SampleSize };
            try { FillRect(sampleDc, ref area, background); }
            finally { DeleteObject(background); }
            previewPixel = uint.MaxValue;
            var dc = GetDC(0);
            if (dc == 0) return;
            try
            {
                // Clip at the virtual desktop edge; blank cells keep the selected pixel centered.
                var left = Math.Max(source.Left, GetSystemMetrics(76));
                var top = Math.Max(source.Top, GetSystemMetrics(77));
                var right = Math.Min(source.Right, GetSystemMetrics(76) + GetSystemMetrics(78));
                var bottom = Math.Min(source.Bottom, GetSystemMetrics(77) + GetSystemMetrics(79));
                if (right > left && bottom > top && BitBlt(sampleDc, left - source.Left, top - source.Top,
                    right - left, bottom - top, dc, left, top, 0x40CC0020)) // SRCCOPY | CAPTUREBLT
                    previewPixel = GetPixel(sampleDc, radius, radius);
            }
            finally { ReleaseDC(0, dc); }
        }

        private void PaintColor(nint dc)
        {
            var background = CreateSolidBrush(0x001C1A18);
            var area = new Rect { Right = 320, Bottom = OverlayHeight };
            try { FillRect(dc, ref area, background); }
            finally { DeleteObject(background); }
            SetStretchBltMode(dc, 3); // COLORONCOLOR: exact nearest-neighbor pixels, no smoothing.
            StretchBlt(dc, 12, 12, LensSize, LensSize, sampleDc, 0, 0, SampleSize, SampleSize, 0x00CC0020);
            // Two contrasting frames keep the center cell visible on every sampled color.
            var cell = new Rect { Left = 12 + SampleSize / 2 * Zoom, Top = 12 + SampleSize / 2 * Zoom,
                Right = 12 + (SampleSize / 2 + 1) * Zoom, Bottom = 12 + (SampleSize / 2 + 1) * Zoom };
            var black = CreateSolidBrush(0);
            var white = CreateSolidBrush(0x00FFFFFF);
            try
            {
                FrameRect(dc, ref cell, black);
                cell.Left--; cell.Top--; cell.Right++; cell.Bottom++;
                FrameRect(dc, ref cell, white);
                var swatch = new Rect { Left = 164, Top = 14, Right = 194, Bottom = 44 };
                var brush = CreateSolidBrush(previewPixel == uint.MaxValue ? 0x003D3730 : previewPixel);
                try { FillRect(dc, ref swatch, brush); FrameRect(dc, ref swatch, white); }
                finally { DeleteObject(brush); }
            }
            finally { DeleteObject(black); DeleteObject(white); }
            SetBkMode(dc, 1); SetTextColor(dc, 0x00F5F4F2);
            var oldFont = SelectObject(dc, GetStockObject(17));
            var hex = previewPixel == uint.MaxValue ? "Недоступно" : ColorHex(previewPixel);
            TextOutW(dc, 204, 22, hex, hex.Length);
            const string click = "Левая кнопка", choose = "Выбрать цвет", cancel = "Esc / правая кнопка", timeout = "Отмена · 30 с", zoom = "9×";
            TextOutW(dc, 164, 63, click, click.Length);
            TextOutW(dc, 164, 82, choose, choose.Length);
            SetTextColor(dc, 0x00BAB1A9);
            TextOutW(dc, 164, 117, cancel, cancel.Length);
            TextOutW(dc, 164, 136, timeout, timeout.Length);
            TextOutW(dc, 12, 147, zoom, zoom.Length);
            SelectObject(dc, oldFont);
        }

        private void RefreshHover()
        {
            if (color || finished || Environment.TickCount64 - lastHover < 150) return;
            lastHover = Environment.TickCount64;
            var root = GetAncestor(WindowFromPoint(cursor), 2);
            if (root == window || (root != 0 && borders.Contains(root)))
            {
                // Some WindowFromPoint paths can still see a layered overlay. Temporarily hide only
                // our own surfaces, without flushing composition or activating any foreign window.
                ShowWindow(window, 0);
                foreach (var border in borders) if (border != 0) ShowWindow(border, 0);
                root = GetAncestor(WindowFromPoint(cursor), 2);
                ShowWindow(window, 4); // SW_SHOWNOACTIVATE
                if (hasHighlight) foreach (var border in borders) ShowWindow(border, 4);
            }
            GetWindowThreadProcessId(root, out var processId);
            if (!hoverIdentities.TryGetValue(root, out var identity) || identity.ProcessId != processId || lastHover - identity.CheckedAt >= 2000)
            {
                ScreenPick? candidate = null;
                try { candidate = ReadWindow(root, owner, verifyExecutable: false); }
                catch (Exception e) when (e is InvalidOperationException or Win32Exception or UnauthorizedAccessException) { }
                identity = new(processId, candidate, lastHover);
                if (hoverIdentities.Count >= 64) hoverIdentities.Clear();
                hoverIdentities[root] = identity;
            }
            if (identity.Candidate is not { } selected || !IsWindow(root))
            {
                hoverLabel = "Окно недоступно для закрепления";
                HideHighlight();
            }
            else
            {
                // A document/window caption is not an application identity. Display only the verified
                // executable filename or package identity returned by the same validation as selection.
                hoverLabel = selected.ExecutablePath is { } executable ? Path.GetFileName(executable)
                    : selected.AppUserModelId!.Split('!')[0].Split('_')[0];
                ShowHighlight(root);
            }
            InvalidateRect(window, 0, false);
        }

        private void ShowHighlight(nint root)
        {
            if (DwmGetWindowAttribute(root, 9, out var bounds, Marshal.SizeOf<Rect>()) < 0 && !GetWindowRect(root, out bounds))
            { HideHighlight(); return; }
            var width = bounds.Right - bounds.Left;
            var height = bounds.Bottom - bounds.Top;
            if (width <= 0 || height <= 0) { HideHighlight(); return; }
            if (hasHighlight && highlightedRoot == root && highlightedBounds.Equals(bounds)) return;
            highlightedRoot = root; highlightedBounds = bounds; hasHighlight = true;
            const int thickness = 3;
            var horizontal = Math.Min(thickness, height);
            var vertical = Math.Min(thickness, width);
            SetWindowPos(borders[0], new nint(-1), bounds.Left, bounds.Top, width, horizontal, 0x0010 | 0x0040);
            SetWindowPos(borders[1], new nint(-1), bounds.Left, bounds.Bottom - horizontal, width, horizontal, 0x0010 | 0x0040);
            SetWindowPos(borders[2], new nint(-1), bounds.Left, bounds.Top, vertical, height, 0x0010 | 0x0040);
            SetWindowPos(borders[3], new nint(-1), bounds.Right - vertical, bounds.Top, vertical, height, 0x0010 | 0x0040);
            foreach (var border in borders) InvalidateRect(border, 0, false);
            SetWindowPos(window, new nint(-1), 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002);
        }

        private void HideHighlight()
        {
            foreach (var border in borders) if (border != 0) ShowWindow(border, 0);
            hasHighlight = false;
            highlightedRoot = 0;
        }

        private static void PaintBorder(nint hwnd)
        {
            var dc = BeginPaint(hwnd, out var paint);
            if (dc == 0) return;
            var brush = CreateSolidBrush(0x00B8D9B5);
            try { if (GetClientRect(hwnd, out var area)) FillRect(dc, ref area, brush); }
            finally { DeleteObject(brush); EndPaint(hwnd, ref paint); }
        }

        private sealed record HoverIdentity(uint ProcessId, ScreenPick? Candidate, long CheckedAt);

        private void Paint(nint hwnd)
        {
            var dc = BeginPaint(hwnd, out var paint);
            if (dc == 0) return;
            if (color)
            {
                try { PaintColor(dc); }
                finally { EndPaint(hwnd, ref paint); }
                return;
            }
            var transparent = CreateSolidBrush(TransparentColor);
            var background = CreateSolidBrush(0x00232220);
            var white = CreatePen(0, 1, 0x00F5F4F2);
            try
            {
                var area = new Rect { Right = 320, Bottom = OverlayHeight };
                FillRect(dc, ref area, transparent);
                var label = new Rect { Left = 28, Top = 30, Right = 314, Bottom = OverlayHeight - 4 };
                FillRect(dc, ref label, background);
                var oldPen = SelectObject(dc, white);
                // Leave the center pixel uncovered even before hiding the overlay.
                MoveToEx(dc, crossX - 11, crossY, 0); LineTo(dc, crossX - 3, crossY);
                MoveToEx(dc, crossX + 3, crossY, 0); LineTo(dc, crossX + 12, crossY);
                MoveToEx(dc, crossX, crossY - 11, 0); LineTo(dc, crossX, crossY - 3);
                MoveToEx(dc, crossX, crossY + 3, 0); LineTo(dc, crossX, crossY + 12);
                SelectObject(dc, oldPen);
                SetBkMode(dc, 1); SetTextColor(dc, 0x00F5F4F2);
                var oldFont = SelectObject(dc, GetStockObject(17));
                var first = hoverLabel;
                var textArea = new Rect { Left = 38, Top = 39, Right = 306, Bottom = 58 };
                DrawTextW(dc, first, first.Length, ref textArea, 0x0020 | 0x8000 | 0x0800); // Single line, ellipsis, no '&' mnemonic.
                const string selection = "Левая кнопка — выбрать окно";
                TextOutW(dc, 38, 62, selection, selection.Length);
                const string second = "Esc / правая кнопка — отмена · 30 с";
                TextOutW(dc, 38, 85, second, second.Length);
                SelectObject(dc, oldFont);
            }
            finally { DeleteObject(transparent); DeleteObject(background); DeleteObject(white); EndPaint(hwnd, ref paint); }
        }
    }

    private static string ColorHex(uint pixel) => $"#{pixel & 255:X2}{(pixel >> 8) & 255:X2}{(pixel >> 16) & 255:X2}";

    private static ScreenPick ReadColor(Point point)
    {
        var dc = GetDC(0);
        if (dc == 0) throw new Win32Exception("Не удалось прочитать цвет экрана.");
        try
        {
            var pixel = GetPixel(dc, point.X, point.Y);
            if (pixel == uint.MaxValue) throw new InvalidOperationException("Цвет этой области экрана недоступен.");
            return new ScreenPick(ColorHex(pixel), null, null, null);
        }
        finally { ReleaseDC(0, dc); }
    }

    private static ScreenPick ReadWindow(Point point, nint owner)
    {
        var target = WindowFromPoint(point);
        var root = GetAncestor(target, 2); // GA_ROOT: do not accidentally select an unrelated owner window.
        return ReadWindow(root, owner);
    }

    private static ScreenPick ReadWindow(nint root, nint owner, bool verifyExecutable = true)
    {
        if (root == 0 || root == owner || root == GetDesktopWindow() || root == GetShellWindow())
            throw new InvalidOperationException("Выберите окно приложения, а не рабочий стол или SideWidget.");
        var className = new StringBuilder(256);
        GetClassNameW(root, className, className.Capacity);
        if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
            throw new InvalidOperationException("Системная поверхность не является приложением. Выберите другое окно.");
        GetWindowThreadProcessId(root, out var processId);
        if (processId == 0 || processId == Environment.ProcessId)
            throw new InvalidOperationException("Сам SideWidget нельзя выбрать этим способом.");
        var process = OpenProcess(0x1000, false, processId); // PROCESS_QUERY_LIMITED_INFORMATION only.
        if (process == 0) throw new InvalidOperationException("Нет доступа к выбранному приложению. Используйте ручной выбор файла.");
        try
        {
            var path = new StringBuilder(32768);
            uint length = (uint)path.Capacity;
            if (!QueryFullProcessImageNameW(process, 0, path, ref length))
                throw new InvalidOperationException("Не удалось определить приложение. Используйте ручной выбор файла.");
            var executable = path.ToString();
            var file = Path.GetFileName(executable);
            // A frame-host PID identifies Windows infrastructure, not the hosted app. Never pin it.
            if (file.Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase) ||
                file.Equals("RuntimeBroker.exe", StringComparison.OrdinalIgnoreCase) ||
                file.Equals("dllhost.exe", StringComparison.OrdinalIgnoreCase) ||
                file.Equals("rundll32.exe", StringComparison.OrdinalIgnoreCase) ||
                file.Equals("svchost.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Окно принадлежит системному хосту. Выберите приложение вручную; хост закреплён не будет.");
            // A document caption is not an application name and may contain private data.
            var name = Path.GetFileNameWithoutExtension(executable);
            uint appLength = 0;
            var status = GetApplicationUserModelId(process, ref appLength, null);
            if (status == 122 && appLength is > 0 and <= 512)
            {
                var appId = new StringBuilder((int)appLength);
                if (GetApplicationUserModelId(process, ref appLength, appId) == 0 && appId.Length > 0)
                    return new ScreenPick(null, null, appId.ToString(), name);
                throw new InvalidOperationException("Не удалось определить идентификатор приложения Microsoft Store. Выберите его вручную.");
            }
            if (status != 15703) // APPMODEL_ERROR_NO_APPLICATION; any other failure is not proof of a Win32 app.
                throw new InvalidOperationException("Не удалось надёжно определить способ запуска приложения. Выберите файл вручную.");
            // Hover trusts the process image query and avoids a potentially slow filesystem probe.
            // The final explicit selection still verifies the file before returning it to the host.
            if (!Path.IsPathFullyQualified(executable) || (verifyExecutable && !File.Exists(executable)))
                throw new InvalidOperationException("Исполняемый файл выбранного окна недоступен.");
            return new ScreenPick(null, executable, null, name);
        }
        finally { CloseHandle(process); }
    }

    private delegate nint WindowProc(nint window, uint message, nint wParam, nint lParam);
    private delegate nint HookProc(int code, nint message, nint data);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public Rect Bounds, WorkArea; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInfo { public Point Position; public uint MouseData, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInfo { public uint Key, ScanCode, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct Message { public nint Window; public uint Id; public nuint WParam; public nint LParam; public uint Time; public Point Position; public uint Private; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
    { public uint Size, Style; public nint Procedure; public int ClassExtra, WindowExtra; public nint Instance, Icon, Cursor, Background; public string? MenuName, ClassName; public nint SmallIcon; }
    [StructLayout(LayoutKind.Sequential)] private struct PaintInfo
    { public nint Dc; [MarshalAs(UnmanagedType.Bool)] public bool Erase; public Rect Paint; [MarshalAs(UnmanagedType.Bool)] public bool Restore; [MarshalAs(UnmanagedType.Bool)] public bool IncrementalUpdate; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Reserved; }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandleW(string? module);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassExW(ref WindowClass definition);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterClassW(string className, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowExW(uint extendedStyle, string className, string name, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetLayeredWindowAttributes(nint window, uint color, byte alpha, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint SetWindowsHookExW(int type, HookProc callback, nint module, uint threadId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError = true)] private static extern nuint SetTimer(nint window, nuint id, uint milliseconds, nint callback);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool KillTimer(nint window, nuint id);
    [DllImport("user32.dll", SetLastError = true)] private static extern int GetMessageW(out Message message, nint window, uint min, uint max);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(ref Message message);
    [DllImport("user32.dll")] private static extern nint DefWindowProcW(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessageW(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern nint LoadCursorW(nint instance, nint name);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool InvalidateRect(nint window, nint rectangle, [MarshalAs(UnmanagedType.Bool)] bool erase);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, uint attribute, out Rect bounds, int size);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out Rect bounds);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(nint window, out Rect bounds);
    [DllImport("user32.dll")] private static extern nint BeginPaint(nint window, out PaintInfo paint);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EndPaint(nint window, ref PaintInfo paint);
    [DllImport("user32.dll")] private static extern int FillRect(nint dc, ref Rect rectangle, nint brush);
    [DllImport("gdi32.dll")] private static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern nint CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool MoveToEx(nint dc, int x, int y, nint previous);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool LineTo(nint dc, int x, int y);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(nint dc, int mode);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(nint dc, uint color);
    [DllImport("gdi32.dll")] private static extern nint GetStockObject(int id);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TextOutW(nint dc, int x, int y, string text, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DrawTextW(nint dc, string text, int length, ref Rect bounds, uint format);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UpdateWindow(nint window);
    [DllImport("user32.dll")] private static extern int FrameRect(nint dc, ref Rect rectangle, nint brush);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(nint dc, int mode);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BitBlt(nint dc, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool StretchBlt(nint dc, int x, int y, int width, int height, nint source, int sourceX, int sourceY, int sourceWidth, int sourceHeight, uint operation);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc, int x, int y);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flag);
    [DllImport("user32.dll")] private static extern nint GetDesktopWindow();
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(nint window, StringBuilder name, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(nint window, StringBuilder name, int length);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageNameW(nint process, uint flags, StringBuilder path, ref uint length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetApplicationUserModelId(nint process, ref uint length, StringBuilder? id);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
}

