using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ClaudeToolbar.Core.Sessions;
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Services;

/// <summary>The chain of processes above a pid, with names, start times and whether each owns a visible window.</summary>
public static class ProcessTable
{
    private const uint TH32CS_SNAPPROCESS = 0x2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    /// <summary>Only the processes on the chain above <paramref name="startPid"/> (never every process's start time).</summary>
    public static IReadOnlyDictionary<int, ProcessRecord> Chain(int startPid)
    {
        var parents = ParentsByPid();
        var windowed = WindowOwners();
        var table = new Dictionary<int, ProcessRecord>();
        var pid = startPid;
        for (var depth = 0; depth <= HostChain.MaxDepth && pid > 0 && !table.ContainsKey(pid); depth++)
        {
            if (!parents.TryGetValue(pid, out var p)) break;
            table[pid] = new ProcessRecord(pid, p.Parent, p.Name, StartTime(pid), windowed.Contains(pid));
            pid = p.Parent;
        }
        return table;
    }

    private static Dictionary<int, (int Parent, string Name)> ParentsByPid()
    {
        var parents = new Dictionary<int, (int Parent, string Name)>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1)) return parents;
        try
        {
            var entry = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            if (!Process32FirstW(snapshot, ref entry)) return parents;
            do
            {
                var name = Path.GetFileNameWithoutExtension(entry.szExeFile).ToLowerInvariant();
                parents[(int)entry.th32ProcessID] = ((int)entry.th32ParentProcessID, name);
            }
            while (Process32NextW(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }
        return parents;
    }

    public static DateTimeOffset StartTime(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.StartTime;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return DateTimeOffset.MinValue;
        }
    }

    /// <summary>Pids owning at least one visible, titled, non-tool top-level window.</summary>
    public static HashSet<int> WindowOwners()
    {
        var owners = new HashSet<int>();
        EnumWindows((hwnd, _) =>
        {
            if (IsCandidateWindow(hwnd))
            {
                GetWindowThreadProcessId(hwnd, out var pid);
                owners.Add((int)pid);
            }
            return true;
        }, IntPtr.Zero);
        return owners;
    }

    public static bool IsCandidateWindow(IntPtr hwnd)
    {
        if (!IsWindowVisible(hwnd) || GetWindowTextLength(hwnd) == 0) return false;
        var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        return (ex & WS_EX_TOOLWINDOW) == 0;
    }
}
