import XCTest
@testable import ClaudeToolbarCore
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

final class OAuthUsageClientTests: XCTestCase {
    private let clock = FakeClock()

    private func client(_ transport: FakeTransport) -> OAuthUsageClient {
        OAuthUsageClient(transport: transport, clock: clock)
    }

    func testSendsExpectedRequest() async {
        let transport = FakeTransport(FakeTransport.response(200, body: "{}"))
        _ = await client(transport).fetch(accessToken: "tok-123")
        let request = transport.lastRequest
        XCTAssertEqual(request?.url?.absoluteString, "https://api.anthropic.com/api/oauth/usage")
        XCTAssertEqual(request?.httpMethod, "GET")
        XCTAssertEqual(request?.value(forHTTPHeaderField: "Authorization"), "Bearer tok-123")
        XCTAssertEqual(request?.value(forHTTPHeaderField: "anthropic-beta"), "oauth-2025-04-20")
        XCTAssertEqual(request?.value(forHTTPHeaderField: "User-Agent"), "claude-code/2.0.0")
        XCTAssertEqual(request?.value(forHTTPHeaderField: "Accept"), "application/json")
        XCTAssertEqual(request?.timeoutInterval, 10)
    }

    func testOkParsesBodyWithClockTime() async {
        let transport = FakeTransport(FakeTransport.response(200, body: #"{ "five_hour": { "utilization": 42 } }"#))
        let result = await client(transport).fetch(accessToken: "t")
        guard case .ok(let snapshot) = result else { return XCTFail("expected ok, got \(result)") }
        XCTAssertEqual(snapshot.fiveHour?.utilization, 42)
        XCTAssertEqual(snapshot.fetchedAt, clock.now)
    }

    func testUnauthorized() async {
        for status in [401, 403] {
            let result = await client(FakeTransport(FakeTransport.response(status))).fetch(accessToken: "t")
            XCTAssertEqual(result, .unauthorized, "status \(status)")
        }
    }

    func testRateLimitedWithDeltaAndDate() async {
        let delta = await client(FakeTransport(FakeTransport.response(429, headers: ["retry-after": "120"]))).fetch(accessToken: "t")
        XCTAssertEqual(delta, .rateLimited(retryAfter: 120))

        let date = clock.now.addingTimeInterval(90)
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "EEE, dd MMM yyyy HH:mm:ss 'GMT'"
        let dated = await client(FakeTransport(FakeTransport.response(429, headers: ["retry-after": formatter.string(from: date)]))).fetch(accessToken: "t")
        guard case .rateLimited(let retry) = dated, let retry else { return XCTFail("expected rateLimited with value, got \(dated)") }
        XCTAssertEqual(retry, 90, accuracy: 1)

        let none = await client(FakeTransport(FakeTransport.response(429))).fetch(accessToken: "t")
        XCTAssertEqual(none, .rateLimited(retryAfter: nil))
    }

    func testOtherStatusFails() async {
        let result = await client(FakeTransport(FakeTransport.response(500, body: "boom"))).fetch(accessToken: "t")
        XCTAssertEqual(result, .failed("HTTP 500"))
    }

    func testTransportErrorFails() async {
        let result = await client(FakeTransport(error: FakeTransportError())).fetch(accessToken: "t")
        guard case .failed(let message) = result else { return XCTFail("expected failed") }
        XCTAssertEqual(message, "socket closed")
    }

    func testTimeoutMessage() async {
        let result = await client(FakeTransport(error: URLError(.timedOut))).fetch(accessToken: "t")
        XCTAssertEqual(result, .failed("Request timed out"))
    }

    func testHTTPDateParsing() {
        XCTAssertEqual(HTTPDate.parse("Wed, 21 Oct 2015 07:28:00 GMT"), Date(timeIntervalSince1970: 1_445_412_480))
        XCTAssertNil(HTTPDate.parse("yesterday"))
    }
}
