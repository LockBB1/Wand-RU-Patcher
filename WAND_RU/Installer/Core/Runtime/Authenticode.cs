using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WandRuInstaller.Core.Runtime;

internal static class Authenticode
{
    public static bool IsTrusted(string path) => IsTrusted(path, null);

    internal static bool IsTrusted(string path, SafeFileHandle? handle)
    {
        var held = false;
        var filePath = IntPtr.Zero;
        var filePointer = IntPtr.Zero;
        try
        {
            handle?.DangerousAddRef(ref held);
            filePath = Marshal.StringToCoTaskMemUni(Path.GetFullPath(path));
            var file = new TrustFile
            {
                Size = (uint)Marshal.SizeOf<TrustFile>(), Path = filePath,
                Handle = handle?.DangerousGetHandle() ?? IntPtr.Zero, Subject = IntPtr.Zero
            };
            filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
            Marshal.StructureToPtr(file, filePointer, false);
            var data = new TrustData
            {
                Size = (uint)Marshal.SizeOf<TrustData>(), Policy = IntPtr.Zero, Sip = IntPtr.Zero,
                Ui = 2, Revocation = 0, Union = 1, File = filePointer,
                StateAction = 1, State = IntPtr.Zero, Url = IntPtr.Zero,
                Flags = 0x1010, Context = 0, SignatureSettings = IntPtr.Zero
            };
            var policy = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            try { return WinVerifyTrust(new IntPtr(-1), ref policy, ref data) == 0; }
            finally { data.StateAction = 2; WinVerifyTrust(new IntPtr(-1), ref policy, ref data); }
        }
        finally
        {
            if (filePointer != IntPtr.Zero) Marshal.FreeHGlobal(filePointer);
            if (filePath != IntPtr.Zero) Marshal.FreeCoTaskMem(filePath);
            if (held) handle!.DangerousRelease();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct TrustFile
    {
        public uint Size;
        public IntPtr Path, Handle, Subject;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct TrustData
    {
        public uint Size;
        public IntPtr Policy, Sip;
        public uint Ui, Revocation, Union;
        public IntPtr File;
        public uint StateAction;
        public IntPtr State, Url;
        public uint Flags, Context;
        public IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true)]
    static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
}
