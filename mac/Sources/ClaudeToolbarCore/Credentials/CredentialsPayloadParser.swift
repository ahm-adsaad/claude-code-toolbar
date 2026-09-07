import Foundation

/// Parses the `{ "claudeAiOauth": { ... } }` payload Claude Code stores in the Keychain or in `.credentials.json`.
public enum CredentialsPayloadParser {
    public static let expiryMargin: TimeInterval = 60

    public static func parse(_ text: String, source: String, now: Date) -> CredentialsState {
        var payload = text.trimmingCharacters(in: .whitespacesAndNewlines)
        if payload.isEmpty { return .invalid(source: source, reason: "Empty credentials") }
        if let decoded = HexText.decode(payload) { payload = decoded }

        guard let object = try? JSONSerialization.jsonObject(with: Data(payload.utf8)),
              let root = object as? [String: Any] else {
            return .invalid(source: source, reason: "Invalid JSON")
        }
        guard let oauth = root["claudeAiOauth"] as? [String: Any] else {
            return .invalid(source: source, reason: "claudeAiOauth section missing")
        }
        guard let token = oauth["accessToken"] as? String, !token.isEmpty else {
            return .invalid(source: source, reason: "accessToken missing")
        }
        guard let expiresRaw = JSONNumber.double(oauth["expiresAt"]) else {
            return .invalid(source: source, reason: "expiresAt missing")
        }
        guard expiresRaw.isFinite, abs(expiresRaw) < 1e15 else {
            return .invalid(source: source, reason: "expiresAt out of range")
        }

        let expiresAt = Date(timeIntervalSince1970: expiresRaw / 1000)
        let subscription = oauth["subscriptionType"] as? String

        if now >= expiresAt.addingTimeInterval(-expiryMargin) {
            return .expired(source: source, expiresAt: expiresAt, subscriptionType: subscription)
        }
        return .valid(source: source, accessToken: token, expiresAt: expiresAt, subscriptionType: subscription)
    }
}

/// `security find-generic-password -w` prints hex when the stored value is not printable text.
enum HexText {
    static func decode(_ text: String) -> String? {
        guard text.count >= 2, text.count % 2 == 0, text.allSatisfy({ $0.isHexDigit }) else { return nil }
        var bytes: [UInt8] = []
        bytes.reserveCapacity(text.count / 2)
        var index = text.startIndex
        while index < text.endIndex {
            let next = text.index(index, offsetBy: 2)
            guard let byte = UInt8(text[index..<next], radix: 16) else { return nil }
            bytes.append(byte)
            index = next
        }
        return String(bytes: bytes, encoding: .utf8)
    }
}
