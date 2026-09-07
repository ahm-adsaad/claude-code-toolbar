import XCTest
@testable import ClaudeToolbarCore

final class CompositeCredentialsSourceTests: XCTestCase {
    private let expires = Date(timeIntervalSince1970: 1_788_782_400 + 3600)

    func testFirstNonMissingWins() {
        let keychain = FakeCredentialsSource(.missing(source: "Keychain"), sourceName: "Keychain")
        let file = FakeCredentialsSource(.valid(source: "/f", accessToken: "t", expiresAt: expires, subscriptionType: nil), sourceName: "/f")
        let composite = CompositeCredentialsSource([keychain, file])
        guard case .valid(let source, _, _, _) = composite.read() else { return XCTFail() }
        XCTAssertEqual(source, "/f")
        XCTAssertEqual(keychain.readCount, 1)
        XCTAssertEqual(file.readCount, 1)
    }

    func testNonMissingStopsTheChain() {
        let keychain = FakeCredentialsSource(.expired(source: "Keychain", expiresAt: expires, subscriptionType: nil), sourceName: "Keychain")
        let file = FakeCredentialsSource(.valid(source: "/f", accessToken: "t", expiresAt: expires, subscriptionType: nil), sourceName: "/f")
        guard case .expired(let source, _, _) = CompositeCredentialsSource([keychain, file]).read() else { return XCTFail() }
        XCTAssertEqual(source, "Keychain")
        XCTAssertEqual(file.readCount, 0)
    }

    func testAllMissingReturnsFirst() {
        let a = FakeCredentialsSource(.missing(source: "A"), sourceName: "A")
        let b = FakeCredentialsSource(.missing(source: "B"), sourceName: "B")
        let composite = CompositeCredentialsSource([a, b])
        guard case .missing(let source) = composite.read() else { return XCTFail() }
        XCTAssertEqual(source, "A")
        XCTAssertEqual(composite.sourceName, "A, B")
    }
}
