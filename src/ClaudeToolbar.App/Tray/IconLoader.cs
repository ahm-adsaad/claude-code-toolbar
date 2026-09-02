using System.Windows;

namespace ClaudeToolbar.App.Tray;

public static class IconLoader
{
    public static System.Drawing.Icon LoadAppIcon(int size)
    {
        var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))
                   ?? throw new InvalidOperationException("app.ico resource missing");
        using var stream = info.Stream;
        return new System.Drawing.Icon(stream, size, size);
    }
}
