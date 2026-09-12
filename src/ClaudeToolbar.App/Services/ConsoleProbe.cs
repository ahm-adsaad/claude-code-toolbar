using System.Text;
using static ClaudeToolbar.App.Interop.NativeMethods;

namespace ClaudeToolbar.App.Services;

/// <summary>
/// What the console of a Claude Code process says about where it is shown: <paramref name="Title"/> is the
/// console title, which a terminal shows as the tab name, and <paramref name="Window"/> is the top-level window
/// that hosts the console — the terminal window for a ConPTY tab, the console window itself for a classic console.
/// </summary>
public sealed record ConsoleInfo(string Title, IntPtr Window);

/// <summary>
/// Reads another process's console by attaching to it for a moment. Windows Terminal parents each tab's
/// pseudo console window to the terminal window that shows it, so the console leads to the exact window,
/// and its title is the tab title — which is how a jump lands on the right tab rather than the window's
/// current one.
/// </summary>
public static class ConsoleProbe
{
    // A process is attached to at most one console at a time, so probes from the listener thread and the UI thread take turns.
    private static readonly object Gate = new();
    private static readonly HandlerRoutine Swallow = _ => true;

    /// <summary>Null when the process has no console (an editor's embedded Claude Code), has gone, or cannot be attached to.</summary>
    public static ConsoleInfo? Read(int pid)
    {
        lock (Gate)
        {
            // While attached this process is one of the console's clients: a Ctrl+C or a tab closing in that
            // instant would otherwise end the toolbar. The handler goes again once detached.
            SetConsoleCtrlHandler(Swallow, true);
            try
            {
                if (!AttachConsole((uint)pid)) return null;
                try
                {
                    var window = GetConsoleWindow();
                    if (window == IntPtr.Zero) return null;
                    var owner = GetAncestor(window, GA_ROOTOWNER);
                    return new ConsoleInfo(Title(), owner == IntPtr.Zero ? window : owner);
                }
                finally
                {
                    FreeConsole();
                }
            }
            finally
            {
                SetConsoleCtrlHandler(Swallow, false);
            }
        }
    }

    private static string Title()
    {
        var text = new StringBuilder(1024);
        var length = GetConsoleTitle(text, (uint)text.Capacity);
        return length == 0 ? string.Empty : text.ToString(0, (int)Math.Min(length, text.Capacity - 1)).Trim();
    }
}
