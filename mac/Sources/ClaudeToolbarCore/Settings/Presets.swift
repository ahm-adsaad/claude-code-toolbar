import Foundation

public enum Presets {
    public static let custom = "custom"
    public static let defaultName = "default"
    public static let names = ["default", "claude", "mono"]

    public static func colors(for name: String) -> (ok: String, warn: String, crit: String)? {
        switch normalizedName(name) {
        case "default": return ("#FF3FB950", "#FFD29922", "#FFF85149")
        case "claude": return ("#FFD97757", "#FFE8A34F", "#FFE5484D")
        case "mono": return ("#FFBDBDBD", "#FF8A8A8A", "#FFFFFFFF")
        default: return nil
        }
    }

    @discardableResult
    public static func apply(_ name: String, to appearance: inout AppearanceSettings) -> Bool {
        guard let colors = colors(for: name) else { return false }
        appearance.barOk = colors.ok
        appearance.barWarn = colors.warn
        appearance.barCrit = colors.crit
        appearance.preset = normalizedName(name)
        return true
    }

    /// The preset whose colours equal the given appearance, or `custom`.
    public static func matching(_ appearance: AppearanceSettings) -> String {
        for name in names {
            if let c = colors(for: name), c.ok == appearance.barOk, c.warn == appearance.barWarn, c.crit == appearance.barCrit {
                return name
            }
        }
        return custom
    }

    static func normalizedName(_ name: String) -> String {
        name.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    }
}
