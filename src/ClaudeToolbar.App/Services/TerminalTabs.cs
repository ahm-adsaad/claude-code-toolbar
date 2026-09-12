using System.Windows.Automation;

namespace ClaudeToolbar.App.Services;

public enum TabSwitch
{
    /// <summary>The window has no tab strip, or a single tab: nothing to switch.</summary>
    NoTabs,
    /// <summary>No tab carries the title (renamed, or retitled between the two reads).</summary>
    NotFound,
    AlreadyCurrent,
    Switched,
}

/// <summary>
/// Brings the tab with a given title to the front of a terminal window through UI Automation. Windows Terminal
/// exposes its tabs as tab items named after their title, and selecting one switches the window to it.
/// </summary>
public static class TerminalTabs
{
    public static TabSwitch Select(IntPtr window, string title)
    {
        if (window == IntPtr.Zero || string.IsNullOrWhiteSpace(title)) return TabSwitch.NoTabs;
        try
        {
            var root = AutomationElement.FromHandle(window);
            var tabs = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
            if (tabs.Count < 2) return TabSwitch.NoTabs;
            foreach (AutomationElement tab in tabs)
            {
                if (!string.Equals(tab.Current.Name?.Trim(), title, StringComparison.Ordinal)) continue;
                if (!tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern) || pattern is not SelectionItemPattern item)
                    return TabSwitch.NotFound;
                if (item.Current.IsSelected) return TabSwitch.AlreadyCurrent;
                item.Select();
                return TabSwitch.Switched;
            }
            return TabSwitch.NotFound;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // The window went away, or its automation tree would not answer: the window still comes forward.
            Log.Error("Tab switch failed", ex);
            return TabSwitch.NotFound;
        }
    }
}
