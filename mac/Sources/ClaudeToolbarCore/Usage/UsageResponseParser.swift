import Foundation

public enum UsageResponseParser {
    public static func parse(_ json: String, fetchedAt: Date) -> UsageResult {
        let trimmed = json.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmed.isEmpty { return .failed("Empty response") }

        let object: Any
        do {
            object = try JSONSerialization.jsonObject(with: Data(trimmed.utf8), options: [.fragmentsAllowed])
        } catch {
            return .failed("Invalid JSON: \(error.localizedDescription)")
        }

        guard let root = object as? [String: Any] else {
            return .failed("Response is not a JSON object")
        }

        return .ok(UsageSnapshot(
            fiveHour: readWindow(root, "five_hour"),
            sevenDay: readWindow(root, "seven_day"),
            sevenDayOpus: readWindow(root, "seven_day_opus"),
            sevenDaySonnet: readWindow(root, "seven_day_sonnet"),
            fetchedAt: fetchedAt))
    }

    private static func readWindow(_ root: [String: Any], _ name: String) -> UsageWindow? {
        guard let element = root[name] as? [String: Any] else { return nil }
        guard let raw = JSONNumber.double(element["utilization"]) else { return nil }
        var resetsAt: Date?
        if let text = element["resets_at"] as? String {
            resetsAt = ISO8601.parse(text)
        }
        return UsageWindow(utilization: min(max(raw, 0), 100), resetsAt: resetsAt)
    }
}
