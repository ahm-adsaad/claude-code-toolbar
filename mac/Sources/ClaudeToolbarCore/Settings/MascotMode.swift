import Foundation

public enum MascotMode {
    public static let full = "full"
    public static let hover = "hover"
    public static let off = "off"
    public static let all = [full, hover, off]

    public static func normalize(_ value: String?) -> String {
        let key = value?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() ?? ""
        return all.contains(key) ? key : full
    }
}
