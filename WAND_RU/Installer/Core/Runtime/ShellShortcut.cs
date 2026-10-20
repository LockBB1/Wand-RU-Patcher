using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace WandRuInstaller.Core.Runtime;

internal static class ShellShortcut
{
    public static (string Target, string Arguments, string Icon, string WorkingDirectory) Read(string path) => WithLink(path, link =>
    {
        var target = new StringBuilder(32768);
        var arguments = new StringBuilder(32768);
        var icon = new StringBuilder(32768);
        var working = new StringBuilder(32768);
        link.GetPath(target, target.Capacity, IntPtr.Zero, 4); // Raw path: never resolve a link over the network.
        link.GetArguments(arguments, arguments.Capacity);
        link.GetIconLocation(icon, icon.Capacity, out var index);
        link.GetWorkingDirectory(working, working.Capacity);
        return (target.ToString(), arguments.ToString(), icon + "," + index, working.ToString());
    });

    public static void Redirect(string? source, string destination, string target, string arguments, string originalExecutable) =>
        WithLink(source, link =>
        {
            var icon = new StringBuilder(32768);
            link.GetIconLocation(icon, icon.Capacity, out _);
            if (icon.Length == 0) link.SetIconLocation(originalExecutable, 0);
            if (source is null) link.SetWorkingDirectory(Path.GetDirectoryName(originalExecutable)!);
            link.SetPath(target);
            link.SetArguments(arguments);
            ((IPersistFile)link).Save(destination, true);
            return true;
        });

    static T WithLink<T>(string? path, Func<IShellLinkW, T> action)
    {
        var type = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"), true)!;
        var instance = Activator.CreateInstance(type)!;
        try
        {
            if (path is not null) ((IPersistFile)instance).Load(Path.GetFullPath(path), 0);
            return action((IShellLinkW)instance);
        }
        finally { Marshal.FinalReleaseComObject(instance); }
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, IntPtr findData, uint flags);
        void GetIDList(out IntPtr list);
        void SetIDList(IntPtr list);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int length);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string value);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int length);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string value);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int length);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string value);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int command);
        void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int length, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string value, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
