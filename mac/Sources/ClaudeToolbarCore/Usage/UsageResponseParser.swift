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
            sevenDayFable: readWindow(root, "seven_day_fable") ?? readScopedWeekly(root, model: "Fable"),
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

    /// Newer models have no `seven_day_<model>` field: their weekly limit is a `weekly_scoped` entry in `limits`,
    /// named by `scope.model.display_name`.
    private static func readScopedWeekly(_ root: [String: Any], model: String) -> UsageWindow? {
        guard let limits = root["limits"] as? [[String: Any]] else { return nil }
        for limit in limits where limit["kind"] as? String == "weekly_scoped" {
            let scope = limit["scope"] as? [String: Any]
            let scopeModel = scope?["model"] as? [String: Any]
            guard let name = scopeModel?["display_name"] as? String,
                  name.caseInsensitiveCompare(model) == .orderedSame,
                  let raw = JSONNumber.double(limit["percent"]) else { continue }
            let resetsAt = (limit["resets_at"] as? String).flatMap(ISO8601.parse)
            return UsageWindow(utilization: min(max(raw, 0), 100), resetsAt: resetsAt)
        }
        return nil
    }
}
