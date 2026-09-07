import Foundation

public enum SettingsValidator {
    public static func isValidColor(_ value: String?) -> Bool {
        guard let value, value.count == 9, value.hasPrefix("#") else { return false }
        return value.dropFirst().allSatisfy { $0.isHexDigit }
    }

    /// Returns a valid `#AARRGGBB`: accepts `#AARRGGBB`, expands `#RRGGBB` to opaque, else the fallback.
    public static func normalizeColor(_ value: String?, fallback: String) -> String {
        if isValidColor(value) { return value!.uppercased() }
        if let value, value.count == 7, value.hasPrefix("#"), value.dropFirst().allSatisfy({ $0.isHexDigit }) {
            return ("#FF" + value.dropFirst()).uppercased()
        }
        return fallback
    }

    public static func normalize(_ input: AppSettings) -> AppSettings {
        var s = input
        let defaults = AppearanceSettings()

        s.appearance.barOk = normalizeColor(s.appearance.barOk, fallback: defaults.barOk)
        s.appearance.barWarn = normalizeColor(s.appearance.barWarn, fallback: defaults.barWarn)
        s.appearance.barCrit = normalizeColor(s.appearance.barCrit, fallback: defaults.barCrit)
        s.appearance.warnThreshold = min(max(s.appearance.warnThreshold, 1), 99)
        s.appearance.critThreshold = min(max(s.appearance.critThreshold, 2), 100)
        if s.appearance.warnThreshold >= s.appearance.critThreshold {
            s.appearance.warnThreshold = defaults.warnThreshold
            s.appearance.critThreshold = defaults.critThreshold
        }
        let preset = Presets.normalizedName(s.appearance.preset)
        s.appearance.preset = (preset.isEmpty || !(Presets.names.contains(preset) || preset == Presets.custom)) ? Presets.custom : preset

        s.rows.barWidth = min(max(s.rows.barWidth, 24), 80)

        s.behavior.refreshIntervalSeconds = min(max(s.behavior.refreshIntervalSeconds, 30), 300)

        s.version = 1
        return s
    }
}
