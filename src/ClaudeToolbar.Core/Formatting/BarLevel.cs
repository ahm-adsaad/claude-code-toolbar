namespace ClaudeToolbar.Core.Formatting;

public enum BarLevel
{
    Ok,
    Warn,
    Crit,
}

public static class BarLevelResolver
{
    public static BarLevel Resolve(double utilization, int warnThreshold, int critThreshold)
    {
        if (utilization < warnThreshold) return BarLevel.Ok;
        if (utilization < critThreshold) return BarLevel.Warn;
        return BarLevel.Crit;
    }
}
