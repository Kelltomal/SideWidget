using System.Diagnostics;
using System.Runtime.InteropServices;
using static SideWidget.App.CoreAudioInterop;

namespace SideWidget.App;

public sealed partial class AudioService : IDisposable
{
    private readonly object gate = new();
    private readonly Notifications notifications = new();
    private IMMDeviceEnumerator? notificationEnumerator;
    private bool disposed;
    public event Action? DevicesChanged;

    public AudioService()
    {
        notifications.Changed = () => { RefreshLevelNotifications(); DevicesChanged?.Invoke(); };
        try { notificationEnumerator = CreateEnumerator(); Check(notificationEnumerator.RegisterEndpointNotificationCallback(notifications)); }
        catch (COMException) { Release(notificationEnumerator); notificationEnumerator = null; }
        BeginLevelNotifications();
    }

    public object Get() { lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); return Snapshot(); } }

    public object Set(double? volume, bool? muted, bool? microphoneMuted, string? sessionId)
    {
        if (volume is { } level && (!double.IsFinite(level) || level < 0 || level > 1)) throw new ArgumentOutOfRangeException(nameof(volume));
        if (sessionId is not null && (sessionId.Length == 0 || sessionId.Length > 4096)) throw new ArgumentException("Некорректная аудиосессия.", nameof(sessionId));
        if (sessionId is not null && microphoneMuted is not null) throw new ArgumentException("Микрофон не относится к сессии воспроизведения.");
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var enumerator = CreateEnumerator();
            try
            {
                if (volume is not null || muted is not null)
                {
                    Check(enumerator.GetDefaultAudioEndpoint(0, 1, out var device));
                    try
                    {
                        if (sessionId is null) WithEndpoint(device, endpoint =>
                        {
                            var context = Guid.Empty;
                            if (volume is { } v) Check(endpoint.SetMasterVolumeLevelScalar((float)v, ref context));
                            if (muted is { } m) Check(endpoint.SetMute(m, ref context));
                        });
                        else
                        {
                            var found = false;
                            VisitSessions(device, (id, session) =>
                            {
                                if (!string.Equals(id, sessionId, StringComparison.Ordinal)) return;
                                var simple = (ISimpleAudioVolume)session;
                                var context = Guid.Empty;
                                if (volume is { } v) Check(simple.SetMasterVolume((float)v, ref context));
                                if (muted is { } m) Check(simple.SetMute(m, ref context));
                                found = true;
                            });
                            if (!found) throw new InvalidOperationException("Аудиосессия завершена или устройство изменилось. Обновите микшер.");
                        }
                    }
                    finally { Release(device); }
                }
                if (microphoneMuted is { } microphone)
                {
                    Check(enumerator.GetDefaultAudioEndpoint(1, 2, out var device));
                    try { WithEndpoint(device, endpoint => { var context = Guid.Empty; Check(endpoint.SetMute(microphone, ref context)); }); }
                    finally { Release(device); }
                }
            }
            finally { Release(enumerator); }
            return Snapshot();
        }
    }

    private object Snapshot()
    {
        IMMDeviceEnumerator? enumerator = null;
        var devices = new List<object>();
        var sessions = new List<object>();
        float? volume = null;
        bool? muted = null, microphoneMuted = null;
        var message = new List<string>();
        try
        {
            enumerator = CreateEnumerator();
            for (var flow = 0; flow <= 1; flow++)
            {
                string? defaultId = null;
                if (enumerator.GetDefaultAudioEndpoint(flow, flow == 0 ? 1 : 2, out var defaultDevice) >= 0)
                {
                    try { Check(defaultDevice.GetId(out defaultId)); }
                    finally { Release(defaultDevice); }
                }
                Check(enumerator.EnumAudioEndpoints(flow, 1, out var collection));
                try
                {
                    Check(collection.GetCount(out var count));
                    for (uint index = 0; index < count; index++)
                    {
                        if (collection.Item(index, out var device) < 0) continue;
                        try
                        {
                            Check(device.GetId(out var id));
                            devices.Add(new { id, name = DeviceName(device, id), flow = flow == 0 ? "output" : "input", @default = id == defaultId });
                        }
                        catch (COMException) { }
                        finally { Release(device); }
                    }
                }
                finally { Release(collection); }
            }
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out var output) >= 0)
            {
                try
                {
                    WithEndpoint(output, endpoint => { Check(endpoint.GetMasterVolumeLevelScalar(out var v)); Check(endpoint.GetMute(out var m)); volume = v; muted = m; });
                    VisitSessions(output, (id, session) =>
                    {
                        try
                        {
                            Check(session.GetState(out var state));
                            if (state == 2) return;
                            var simple = (ISimpleAudioVolume)session;
                            Check(simple.GetMasterVolume(out var v)); Check(simple.GetMute(out var m));
                            Check(session.GetDisplayName(out var name));
                            Check(session.GetProcessId(out var pid));
                            if (session.IsSystemSoundsSession() == 0) name = "Системные звуки";
                            else if (string.IsNullOrWhiteSpace(name) || name.StartsWith('@'))
                            {
                                try { using var process = Process.GetProcessById((int)pid); name = process.ProcessName; }
                                catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { name = "Приложение " + pid; }
                            }
                            sessions.Add(new { id, name, volume = v, muted = m });
                        }
                        catch (COMException) { }
                    });
                }
                catch (COMException) { message.Add("Устройство вывода изменилось. Обновите состояние."); }
                finally { Release(output); }
            }
            else message.Add("Нет устройства вывода по умолчанию.");
            if (enumerator.GetDefaultAudioEndpoint(1, 2, out var input) >= 0)
            {
                try { WithEndpoint(input, endpoint => { Check(endpoint.GetMute(out var m)); microphoneMuted = m; }); }
                catch (COMException) { message.Add("Состояние микрофона недоступно."); }
                finally { Release(input); }
            }
        }
        catch (COMException) { message.Add("Core Audio недоступен."); }
        finally { Release(enumerator); }
        return new { available = volume.HasValue, volume, muted, microphoneMuted, devices, sessions,
            message = message.Count > 0 ? string.Join(" ", message) : null };
    }

    private static string DeviceName(IMMDevice device, string fallback)
    {
        if (device.OpenPropertyStore(0, out var store) < 0) return fallback;
        try
        {
            var key = new PropertyKey { Format = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Id = 14 };
            if (store.GetValue(ref key, out var value) < 0) return fallback;
            try { return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) ?? fallback : fallback; }
            finally { PropVariantClear(ref value); }
        }
        finally { Release(store); }
    }

    private static void WithEndpoint(IMMDevice device, Action<IAudioEndpointVolume> action)
    {
        var iid = EndpointVolume;
        Check(device.Activate(ref iid, 23, IntPtr.Zero, out var value));
        try { action((IAudioEndpointVolume)value); }
        finally { Release(value); }
    }

    private static void VisitSessions(IMMDevice device, Action<string, IAudioSessionControl2> action)
    {
        Check(device.GetId(out var deviceId));
        var iid = SessionManager;
        Check(device.Activate(ref iid, 23, IntPtr.Zero, out var manager));
        try
        {
            Check(((IAudioSessionManager2)manager).GetSessionEnumerator(out var enumerator));
            try
            {
                Check(enumerator.GetCount(out var count));
                for (var index = 0; index < count; index++)
                {
                    if (enumerator.GetSession(index, out var session) < 0) continue;
                    try { if (session.GetSessionInstanceIdentifier(out var id) >= 0) action(deviceId + "|" + id, session); }
                    finally { Release(session); }
                }
            }
            finally { Release(enumerator); }
        }
        finally { Release(manager); }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            EndLevelNotifications();
            disposed = true;
            notifications.Changed = null;
            if (notificationEnumerator is not null) { notificationEnumerator.UnregisterEndpointNotificationCallback(notifications); Release(notificationEnumerator); notificationEnumerator = null; }
        }
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class Notifications : IMMNotificationClient
    {
        internal Action? Changed;
        private int Notify() { try { Changed?.Invoke(); } catch { /* Never propagate managed exceptions across a native callback. */ } return 0; }
        public int OnDeviceStateChanged(string id, uint state) => Notify();
        public int OnDeviceAdded(string id) => Notify();
        public int OnDeviceRemoved(string id) => Notify();
        public int OnDefaultDeviceChanged(int flow, int role, string? id) => Notify();
        public int OnPropertyValueChanged(string id, PropertyKey key) => Notify();
    }
}
