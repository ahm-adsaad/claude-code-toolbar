import Foundation

/// Accepts `2026-09-07T14:13:00+00:00`, `...Z`, and the same with fractional seconds.
enum ISO8601 {
    private static let plain: ISO8601DateFormatter = {
        let f = ISO8601DateFormatter()
        f.formatOptions = [.withInternetDateTime]
        return f
    }()

    private static let fractional: ISO8601DateFormatter = {
        let f = ISO8601DateFormatter()
        f.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return f
    }()

    static func parse(_ text: String) -> Date? {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        return plain.date(from: trimmed) ?? fractional.date(from: trimmed)
    }
}
