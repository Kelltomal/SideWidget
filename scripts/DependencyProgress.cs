using System;
using System.Runtime.InteropServices;

internal sealed class DependencyProgress : IDisposable
{
    private const uint HideTimeRemaining = 0x4;
    private readonly IProgressDialog dialog;

    internal DependencyProgress()
    {
        var type = Type.GetTypeFromCLSID(new Guid("F8383852-FCD3-11D1-A6B9-006097DF5BD4"));
        dialog = (IProgressDialog)Activator.CreateInstance(type);
        dialog.SetTitle("SideWidget Lite");
        dialog.StartProgressDialog(IntPtr.Zero, IntPtr.Zero, HideTimeRemaining, IntPtr.Zero);
    }

    internal void Stage(string message)
    {
        dialog.SetLine(1, message, false, IntPtr.Zero);
        Report(0, 1);
    }

    internal void Report(long completed, long total)
    {
        if (dialog.HasUserCancelled()) throw new OperationCanceledException();
        dialog.SetProgress64((ulong)completed, (ulong)total);
    }

    public void Dispose()
    {
        dialog.StopProgressDialog();
        Marshal.ReleaseComObject(dialog);
    }

    [ComImport, Guid("EBBC7C04-315E-11D2-B62F-006097DF5BD4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IProgressDialog
    {
        void StartProgressDialog(IntPtr parent, IntPtr enableModeless, uint flags, IntPtr reserved);
        void StopProgressDialog();
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetAnimation(IntPtr module, uint resourceId);
        [PreserveSig]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool HasUserCancelled();
        void SetProgress(uint completed, uint total);
        void SetProgress64(ulong completed, ulong total);
        void SetLine(uint line, [MarshalAs(UnmanagedType.LPWStr)] string text,
            [MarshalAs(UnmanagedType.Bool)] bool compactPath, IntPtr reserved);
    }
}
