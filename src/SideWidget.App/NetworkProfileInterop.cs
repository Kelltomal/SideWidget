using System.Runtime.InteropServices;

namespace SideWidget.App;

static class NetworkProfileInterop
{
    // GUID return values are not Automation VARIANTs. IDispatch/dynamic fails
    // on GetAdapterId; invoke its typed COM vtable instead.
    public static Guid AdapterId(object connection) => ((INetworkConnection)connection).GetAdapterId();

    [ComImport, Guid("DCB00005-570F-4A9B-8D69-199FDBA5723B"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    interface INetworkConnection
    {
        [return: MarshalAs(UnmanagedType.IDispatch)] object GetNetwork();
        bool IsConnectedToInternet { [return: MarshalAs(UnmanagedType.VariantBool)] get; }
        bool IsConnected { [return: MarshalAs(UnmanagedType.VariantBool)] get; }
        int GetConnectivity();
        Guid GetConnectionId();
        Guid GetAdapterId();
        int GetDomainType();
    }
}
