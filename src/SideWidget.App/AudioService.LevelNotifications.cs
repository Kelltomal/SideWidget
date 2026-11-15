using System.Runtime.InteropServices;
using static SideWidget.App.CoreAudioInterop;

namespace SideWidget.App;

public sealed partial class AudioService
{
    public event Action<float, bool>? LevelChanged;
    private LevelSubscription? levelSubscription;
    private System.Threading.Timer? levelRetryTimer;
    private int levelNotificationsStopped = 1, levelRefreshQueued;

    private void BeginLevelNotifications()
    {
        Volatile.Write(ref levelNotificationsStopped, 0);
        RefreshLevelNotifications();
    }

    // Device callbacks only enqueue. COM registration never runs within IMMNotificationClient.
    private void RefreshLevelNotifications()
    {
        if (Volatile.Read(ref levelNotificationsStopped) != 0 || Interlocked.Exchange(ref levelRefreshQueued, 1) != 0) return;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            // A new notification while reconnecting may schedule another pass.
            Interlocked.Exchange(ref levelRefreshQueued, 0);
            lock (gate)
            {
                if (disposed || Volatile.Read(ref levelNotificationsStopped) != 0) return;
                IMMDeviceEnumerator? enumerator = null;
                IMMDevice? device = null;
                IAudioEndpointVolume? endpoint = null;
                LevelSubscription? candidate = null;
                try
                {
                    enumerator = CreateEnumerator();
                    Check(enumerator.GetDefaultAudioEndpoint(0, 1, out device));
                    Check(device.GetId(out var id));
                    if (levelSubscription?.DeviceId == id)
                    {
                        // Device-id equality alone does not prove an old endpoint survived restart.
                        Check(levelSubscription.Endpoint.GetMasterVolumeLevelScalar(out var currentVolume));
                        Check(levelSubscription.Endpoint.GetMute(out var currentMuted));
                        return;
                    }
                    RetireLevelSubscription();
                    var iid = EndpointVolume;
                    Check(device.Activate(ref iid, 23, IntPtr.Zero, out var activated));
                    endpoint = (IAudioEndpointVolume)activated;
                    candidate = new LevelSubscription(this, endpoint, id);
                    endpoint = null; // candidate owns the endpoint even if registration fails
                    candidate.Register();
                    levelSubscription = candidate;
                    Check(candidate.Endpoint.GetMasterVolumeLevelScalar(out var volume));
                    Check(candidate.Endpoint.GetMute(out var muted));
                    candidate.EnqueueInitial(volume, muted);
                    levelRetryTimer?.Dispose(); levelRetryTimer = null;
                }
                catch (COMException)
                {
                    // Unplugged/default-missing devices are expected. Retry without UI polling.
                    RetireLevelSubscription();
                    candidate?.Retire();
                    levelRetryTimer?.Dispose();
                    levelRetryTimer = new System.Threading.Timer(_ => RefreshLevelNotifications(), null, 1000, Timeout.Infinite);
                }
                finally { Release(endpoint); Release(device); Release(enumerator); }
            }
        });
    }

    private void EndLevelNotifications()
    {
        // Caller may already hold gate (Dispose); Monitor is reentrant.
        lock (gate)
        {
            Volatile.Write(ref levelNotificationsStopped, 1);
            levelRetryTimer?.Dispose(); levelRetryTimer = null;
            RetireLevelSubscription();
        }
    }

    private void RetireLevelSubscription()
    {
        var previous = levelSubscription;
        levelSubscription = null;
        previous?.Retire();
    }

    private void DeliverLevel(LevelSubscription subscription, float volume, bool muted)
    {
        lock (gate)
        {
            if (disposed || Volatile.Read(ref levelNotificationsStopped) != 0 || !ReferenceEquals(levelSubscription, subscription)) return;
            // Controller handler only queues UI work. Isolate subscribers from each other.
            var handlers = LevelChanged;
            if (handlers is null) return;
            foreach (Action<float, bool> handler in handlers.GetInvocationList())
                try { handler(volume, muted); } catch { /* Event consumers cannot fault the notification worker. */ }
        }
    }

    [ComVisible(true), Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(IntPtr notificationData);
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class EndpointLevelCallback : IAudioEndpointVolumeCallback
    {
        internal Action<float, bool>? Changed;
        public int OnNotify(IntPtr notificationData)
        {
            try
            {
                if (notificationData == IntPtr.Zero) return 0;
                // AUDIO_VOLUME_NOTIFICATION_DATA: GUID, Win32 BOOL, float, UINT, float[].
                // Copy scalars before native returns; never retain its temporary buffer.
                var muted = Marshal.ReadInt32(notificationData, 16) != 0;
                var volume = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(notificationData, 20));
                if (float.IsFinite(volume)) Volatile.Read(ref Changed)?.Invoke(Math.Clamp(volume, 0, 1), muted);
            }
            catch { /* Never propagate managed exceptions over the native COM boundary. */ }
            return 0;
        }
    }

    private sealed class LevelSubscription
    {
        private readonly AudioService owner;
        private readonly object pendingGate = new();
        private readonly EndpointLevelCallback callback = new();
        private IntPtr callbackPointer;
        private bool retired, queued;
        private long version;
        private float volume;
        private bool muted;
        internal IAudioEndpointVolume Endpoint { get; }
        internal string DeviceId { get; }

        internal LevelSubscription(AudioService owner, IAudioEndpointVolume endpoint, string id)
        {
            this.owner = owner; Endpoint = endpoint; DeviceId = id;
            callback.Changed = Enqueue;
        }

        internal void Register()
        {
            callbackPointer = Marshal.GetComInterfaceForObject(callback, typeof(IAudioEndpointVolumeCallback));
            Check(Endpoint.RegisterControlChangeNotify(callbackPointer));
        }

        internal void Enqueue(float value, bool isMuted)
        {
            if (!float.IsFinite(value)) return;
            lock (pendingGate)
            {
                if (retired) return;
                volume = Math.Clamp(value, 0, 1); muted = isMuted; version++;
                if (queued) return;
                queued = true;
            }
            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (true)
                {
                    float currentVolume; bool currentMuted; long currentVersion;
                    lock (pendingGate)
                    {
                        if (retired) { queued = false; return; }
                        currentVolume = volume; currentMuted = muted; currentVersion = version;
                    }
                    owner.DeliverLevel(this, currentVolume, currentMuted);
                    lock (pendingGate)
                    {
                        if (retired || currentVersion == version) { queued = false; return; }
                    }
                }
            });
        }

        internal void EnqueueInitial(float value, bool isMuted)
        {
            lock (pendingGate)
            {
                // A native change received during initial getters is more recent than that
                // snapshot. Do not let the initial read overwrite the callback's new level.
                if (version == 0) Enqueue(value, isMuted);
            }
        }

        internal void Retire()
        {
            lock (pendingGate) { if (retired) return; retired = true; }
            Volatile.Write(ref callback.Changed, null);
            // Unregister/release off UI and outside native callback. Locals/closure retain both
            // callback and endpoint until COM no longer owns the callback interface.
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { if (callbackPointer != IntPtr.Zero) Endpoint.UnregisterControlChangeNotify(callbackPointer); }
                catch (COMException) { }
                finally
                {
                    if (callbackPointer != IntPtr.Zero) { Marshal.Release(callbackPointer); callbackPointer = IntPtr.Zero; }
                    Release(Endpoint);
                    GC.KeepAlive(callback);
                }
            });
        }
    }
}
