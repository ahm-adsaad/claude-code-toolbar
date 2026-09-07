import Foundation
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

public protocol UsageClient: Sendable {
    func fetch(accessToken: String) async -> UsageResult
}

public struct OAuthUsageClient: UsageClient {
    public static let endpoint = URL(string: "https://api.anthropic.com/api/oauth/usage")!
    public static let userAgent = "claude-code/2.0.0"
    public static let betaHeader = "oauth-2025-04-20"
    public static let requestTimeout: TimeInterval = 10

    private let transport: any HTTPTransport
    private let clock: any ClockSource

    public init(transport: any HTTPTransport, clock: any ClockSource) {
        self.transport = transport
        self.clock = clock
    }

    public func fetch(accessToken: String) async -> UsageResult {
        var request = URLRequest(url: Self.endpoint, cachePolicy: .reloadIgnoringLocalCacheData, timeoutInterval: Self.requestTimeout)
        request.httpMethod = "GET"
        request.setValue("Bearer \(accessToken)", forHTTPHeaderField: "Authorization")
        request.setValue(Self.betaHeader, forHTTPHeaderField: "anthropic-beta")
        request.setValue(Self.userAgent, forHTTPHeaderField: "User-Agent")
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        let response: HTTPResponse
        do {
            response = try await transport.send(request)
        } catch {
            return .failed(Self.describe(error))
        }

        switch response.statusCode {
        case 401, 403:
            return .unauthorized
        case 429:
            return .rateLimited(retryAfter: Self.retryAfter(response.headers["retry-after"], now: clock.now))
        case 200..<300:
            return UsageResponseParser.parse(String(decoding: response.body, as: UTF8.self), fetchedAt: clock.now)
        default:
            return .failed("HTTP \(response.statusCode)")
        }
    }

    static func retryAfter(_ header: String?, now: Date) -> TimeInterval? {
        guard let header = header?.trimmingCharacters(in: .whitespaces), !header.isEmpty else { return nil }
        if let seconds = Double(header) { return max(seconds, 0) }
        if let date = HTTPDate.parse(header) { return date.timeIntervalSince(now) }
        return nil
    }

    static func describe(_ error: Error) -> String {
        if let urlError = error as? URLError, urlError.code == .timedOut { return "Request timed out" }
        return error.localizedDescription
    }
}
