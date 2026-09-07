import XCTest
@testable import ClaudeToolbarCore

final class CredentialsPayloadParserTests: XCTestCase {
    private let now = Date(timeIntervalSince1970: 1_788_782_400)
    private let token = "sk-ant-oat01-SECRET-TOKEN-VALUE"

    private func payload(expiresAtMs: Int64, subscription: String? = "max") -> String {
        let sub = subscription.map { "\"\($0)\"" } ?? "null"
        return """
        { "claudeAiOauth": { "accessToken": "\(token)", "refreshToken": "sk-ant-ort01-x",
          "expiresAt": \(expiresAtMs), "scopes": ["user:profile"], "subscriptionType": \(sub) } }
        """
    }

    func testValid() {
        let expires = Int64(now.timeIntervalSince1970 * 1000) + 3_600_000
        let state = CredentialsPayloadParser.parse(payload(expiresAtMs: expires), source: "Keychain", now: now)
        guard case .valid(let source, let accessToken, let expiresAt, let subscription) = state else { return XCTFail("expected valid, got \(state)") }
        XCTAssertEqual(source, "Keychain")
        XCTAssertEqual(accessToken, token)
        XCTAssertEqual(expiresAt.timeIntervalSince1970, Double(expires) / 1000, accuracy: 0.001)
        XCTAssertEqual(subscription, "max")
    }

    func testExpiredAndMarginBoundary() {
        let nowMs = Int64(now.timeIntervalSince1970 * 1000)
        guard case .expired = CredentialsPayloadParser.parse(payload(expiresAtMs: nowMs - 1000), source: "f", now: now) else { return XCTFail("past should be expired") }
        guard case .expired = CredentialsPayloadParser.parse(payload(expiresAtMs: nowMs + 60_000), source: "f", now: now) else { return XCTFail("exactly at margin should be expired") }
        guard case .valid = CredentialsPayloadParser.parse(payload(expiresAtMs: nowMs + 61_000), source: "f", now: now) else { return XCTFail("just past margin should be valid") }
    }

    func testNullSubscriptionIsAllowed() {
        let expires = Int64(now.timeIntervalSince1970 * 1000) + 3_600_000
        guard case .valid(_, _, _, let subscription) = CredentialsPayloadParser.parse(payload(expiresAtMs: expires, subscription: nil), source: "f", now: now) else { return XCTFail() }
        XCTAssertNil(subscription)
    }

    func testHexEncodedPayloadIsDecoded() {
        let expires = Int64(now.timeIntervalSince1970 * 1000) + 3_600_000
        let hex = payload(expiresAtMs: expires).utf8.map { String(format: "%02X", $0) }.joined()
        guard case .valid(_, let accessToken, _, _) = CredentialsPayloadParser.parse(hex + "\n", source: "Keychain", now: now) else { return XCTFail("hex payload should parse") }
        XCTAssertEqual(accessToken, token)
    }

    func testInvalidShapes() {
        func reason(_ text: String) -> String? {
            if case .invalid(_, let reason) = CredentialsPayloadParser.parse(text, source: "f", now: now) { return reason }
            return nil
        }
        XCTAssertEqual(reason(""), "Empty credentials")
        XCTAssertEqual(reason("not json"), "Invalid JSON")
        XCTAssertEqual(reason("{}"), "claudeAiOauth section missing")
        XCTAssertEqual(reason(#"{ "claudeAiOauth": { "expiresAt": 1 } }"#), "accessToken missing")
        XCTAssertEqual(reason(#"{ "claudeAiOauth": { "accessToken": "" , "expiresAt": 1 } }"#), "accessToken missing")
        XCTAssertEqual(reason(#"{ "claudeAiOauth": { "accessToken": "t" } }"#), "expiresAt missing")
        XCTAssertEqual(reason(#"{ "claudeAiOauth": { "accessToken": "t", "expiresAt": "soon" } }"#), "expiresAt missing")
        XCTAssertEqual(reason(#"{ "claudeAiOauth": { "accessToken": "t", "expiresAt": 1e300 } }"#), "expiresAt out of range")
    }

    func testDescriptionNeverContainsToken() {
        let expires = Int64(now.timeIntervalSince1970 * 1000) + 3_600_000
        let valid = CredentialsPayloadParser.parse(payload(expiresAtMs: expires), source: "Keychain", now: now)
        XCTAssertFalse(valid.description.contains(token))
        XCTAssertFalse("\(valid)".contains("SECRET"))
        XCTAssertTrue(valid.description.contains("Keychain"))
    }
}
