using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace ClaudeToolbar.App.Services;

/// <summary>
/// The Start menu entry and the optional desktop shortcut, both named "Claude Toolbar". Like the Run key, they follow
/// whichever exe was started last, so moving or rebuilding the exe never leaves them pointing at a missing file.
/// </summary>
public static class AppShortcuts
{
    private const string FileName = "Claude Toolbar.lnk";
    private const string Description = "Claude subscription usage in the taskbar";

    public static string StartMenuPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), FileName);
    public static string DesktopPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), FileName);

    /// <summary>Creates or retargets the Start menu entry, and the desktop shortcut when it exists or <paramref name="createDesktop"/> is set.</summary>
    public static void Refresh(bool createDesktop)
    {
        Ensure(StartMenuPath);
        if (createDesktop || HasDesktop()) Ensure(DesktopPath);
    }

    public static bool HasDesktop() => File.Exists(DesktopPath);

    public static void SetDesktop(bool enabled)
    {
        if (enabled) Ensure(DesktopPath);
        else File.Delete(DesktopPath);
    }

    private static void Ensure(string path)
    {
        var exe = StartupRegistration.ExePath;
        if (File.Exists(path) && string.Equals(ReadTarget(path), exe, StringComparison.OrdinalIgnoreCase)) return;
        Write(path, exe);
        Log.Info($"Shortcut {path} -> {exe}");
    }

    private static string? ReadTarget(string path)
    {
        var link = (IShellLinkW)new ShellLink();
        try
        {
            ((IPersistFile)link).Load(path, 0);
            var target = new StringBuilder(1024);
            link.GetPath(target, target.Capacity, IntPtr.Zero, SLGP_RAWPATH);
            return target.ToString();
        }
        catch (COMException) { return null; }
        finally { Marshal.FinalReleaseComObject(link); }
    }

    private static void Write(string path, string exe)
    {
        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(exe);
            link.SetWorkingDirectory(Path.GetDirectoryName(exe) ?? string.Empty);
            link.SetIconLocation(exe, 0);
            link.SetDescription(Description);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            ((IPersistFile)link).Save(path, true);
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }

    private const uint SLGP_RAWPATH = 0x4;

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
