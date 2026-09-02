using ClaudeToolbar.Core.Layout;

namespace ClaudeToolbar.App.Interop;

/// <summary>Snapshot of where the primary taskbar and its notification area are, in physical pixels.</summary>
/// <param name="Taskbar">Docked rect from the AppBar API (where the taskbar lives when shown).</param>
/// <param name="TaskbarNow">Current window rect of Shell_TrayWnd (moves during auto-hide animation).</param>
/// <param name="Notify">Current rect of TrayNotifyWnd.</param>
public sealed record TaskbarLayout(
    IntPtr TrayHwnd,
    IntPtr NotifyHwnd,
    RectI Taskbar,
    RectI TaskbarNow,
    RectI Notify,
    RectI Monitor,
    bool AutoHide,
    uint ExplorerPid);
