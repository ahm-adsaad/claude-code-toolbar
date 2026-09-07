import AppKit
import ClaudeToolbarCore

extension NSColor {
    /// Parses `#AARRGGBB` (the settings file format).
    convenience init?(argbHex: String) {
        guard SettingsValidator.isValidColor(argbHex), let value = UInt32(argbHex.dropFirst(), radix: 16) else { return nil }
        let a = CGFloat((value >> 24) & 0xFF) / 255
        let r = CGFloat((value >> 16) & 0xFF) / 255
        let g = CGFloat((value >> 8) & 0xFF) / 255
        let b = CGFloat(value & 0xFF) / 255
        self.init(srgbRed: r, green: g, blue: b, alpha: a)
    }

    var argbHex: String {
        let color = usingColorSpace(.sRGB) ?? self
        func byte(_ component: CGFloat) -> UInt32 { UInt32((min(max(component, 0), 1) * 255).rounded()) }
        let value = (byte(color.alphaComponent) << 24) | (byte(color.redComponent) << 16) | (byte(color.greenComponent) << 8) | byte(color.blueComponent)
        return String(format: "#%08X", value)
    }
}
