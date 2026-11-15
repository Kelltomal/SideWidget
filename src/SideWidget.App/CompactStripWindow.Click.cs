using System.Diagnostics;
using System.Runtime.InteropServices;
using SideWidget.Core;

namespace SideWidget.App;

internal sealed partial class CompactStripWindow
{
    const nuint ClickTimerId = 0x434C4943;
    const nuint ReplayTag = 0x5357434C;
    const uint LeftDown = 0x0201, LeftUp = 0x0202;
    const uint RightDown = 0x0204, RightUp = 0x0205;
    const uint MoveOnVirtualDesktop = 0xC001;

    readonly Stopwatch clickWatch = Stopwatch.StartNew();
    Native.Point? pendingDown, pendingUp;
    uint pendingRelease, swallowRelease;
    long pendingSince;

    nint ObserveMouse(int code, nint message, nint data)
    {
        if (code >= 0 && (uint)message is LeftDown or LeftUp or RightDown or RightUp)
        {
            var mouse = Marshal.PtrToStructure<MouseInfo>(data);
            if (mouse.Extra != ReplayTag)
            {
                if ((uint)message == swallowRelease)
                {
                    swallowRelease = 0;
                    if (!visible) StopObserving();
                    return 1;
                }
                if ((uint)message == pendingRelease && pendingDown is not null && pendingUp is null)
                {
                    pendingUp = mouse.Position;
                    return 1;
                }
                if ((uint)message is LeftDown or RightDown && visible && BufferClick(mouse.Position, message == RightDown))
                    return 1;
            }
        }
        return CallNextHookEx(mouseHook, code, message, data);
    }

    bool BufferClick(Native.Point point, bool right)
    {
        if (swallowRelease != 0 || (pendingDown is not null && pendingUp is null) ||
            point.X < left || point.X >= left + width || point.Y < top || point.Y >= top + height)
        {
            panel.ResetClicks();
            return false;
        }

        var now = clickWatch.ElapsedMilliseconds;
        var release = right ? RightUp : LeftUp;
        if (ringing && !right)
        {
            panel.ResetClicks();
            ReplayPendingClick();
            swallowRelease = release;
            dismissSignal();
            return true;
        }
        if (panel.Click(now, point.X, point.Y, Native.GetSystemMetrics(36), Native.GetSystemMetrics(37), right))
        {
            ClearPendingClick();
            swallowRelease = release;
            if (right) updateClockMode();
            else open();
            return true;
        }

        ReplayPendingClick();
        if (SetTimer(Handle, ClickTimerId, CompactPanel.DoubleClickTime, 0) == 0)
        {
            panel.ResetClicks();
            return false;
        }
        pendingDown = point;
        pendingRelease = release;
        pendingSince = now;
        return true;
    }

    void ExpirePendingClick()
    {
        if (pendingDown is null) return;
        var remaining = CompactPanel.DoubleClickTime - (clickWatch.ElapsedMilliseconds - pendingSince);
        if (remaining > 0 && SetTimer(Handle, ClickTimerId, (uint)remaining, 0) != 0) return;
        panel.ResetClicks();
        ReplayPendingClick();
        if (!visible) StopObserving();
    }

    void ClearPendingClick()
    {
        KillTimer(Handle, ClickTimerId);
        pendingDown = pendingUp = null;
    }

    void ReplayPendingClick()
    {
        if (pendingDown is not { } down) return;
        var up = pendingUp;
        var right = pendingRelease == RightUp;
        ClearPendingClick();

        var originX = Native.GetSystemMetrics(76);
        var originY = Native.GetSystemMetrics(77);
        var desktopWidth = Native.GetSystemMetrics(78);
        var desktopHeight = Native.GetSystemMetrics(79);
        var inputs = new List<MouseInput>(3) { At(down, right ? 0x0008u : 0x0002u) };
        if (up is { } release) inputs.Add(At(release, right ? 0x0010u : 0x0004u));
        if (Native.GetCursorPos(out var cursor) &&
            (cursor.X != (up ?? down).X || cursor.Y != (up ?? down).Y))
            inputs.Add(At(cursor, 0));

        if (SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<MouseInput>()) != inputs.Count)
            Debug.WriteLine("SideWidget could not replay the buffered click.");

        MouseInput At(Native.Point point, uint button) => new()
        {
            Mouse = new()
            {
                X = Normalize(point.X - originX, desktopWidth),
                Y = Normalize(point.Y - originY, desktopHeight),
                Flags = MoveOnVirtualDesktop | button,
                Extra = ReplayTag
            }
        };
    }

    static int Normalize(int pixel, int size) =>
        (int)Math.Clamp(((long)pixel * 65536 + size - 1) / size, 0, 65535);

    void StopObserving()
    {
        if (mouseHook == 0) return;
        UnhookWindowsHookEx(mouseHook);
        mouseHook = 0;
        panel.ResetClicks();
    }

    // INPUT with its MOUSEINPUT union member. Mouse input is the largest member.
    [StructLayout(LayoutKind.Sequential)]
    struct MouseInput
    {
        public uint Type;
        public MouseInputData Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MouseInputData
    {
        public int X, Y;
        public uint Data, Flags, Time;
        public nuint Extra;
    }

    [DllImport("user32.dll")] static extern uint SendInput(uint count, [In] MouseInput[] inputs, int size);
    [DllImport("user32.dll")] static extern nuint SetTimer(nint window, nuint id, uint interval, nint callback);
    [DllImport("user32.dll")] static extern bool KillTimer(nint window, nuint id);
}
