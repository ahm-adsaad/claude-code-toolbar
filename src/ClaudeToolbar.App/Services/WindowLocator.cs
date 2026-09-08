using System.Text;
using ClaudeToolbar.Core.Sessions;
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Services;

/// <summary>Top-level windows of a host process. EnumWindows walks the Z order from the top, so the first hit is the frontmost.</summary>
public static class WindowLocator
{
    public static IntPtr Find(SessionHost host, string sessionName)
    {
        var windows = new List<(IntPtr Handle, string Title)>();
        EnumWindows((hwnd, _) =>
        {
            if (!ProcessTable.IsCandidateWindow(hwnd)) return true;
            GetWindowThreadProcessId(hwnd, out var pid);
            if ((int)pid == host.Pid) windows.Add((hwnd, Title(hwnd)));
            return true;
        }, IntPtr.Zero);
        if (windows.Count == 0) return IntPtr.Zero;
        // Several windows (two VS Code windows, say): the one whose title names the project wins.
        var named = windows.FirstOrDefault(w => w.Title.Contains(sessionName, StringComparison.OrdinalIgnoreCase));
        return named.Handle != IntPtr.Zero ? named.Handle : windows[0].Handle;
    }

    private static string Title(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length == 0) return string.Empty;
        var text = new StringBuilder(length + 1);
        _ = GetWindowText(hwnd, text, text.Capacity);
        return text.ToString();
    }
}
