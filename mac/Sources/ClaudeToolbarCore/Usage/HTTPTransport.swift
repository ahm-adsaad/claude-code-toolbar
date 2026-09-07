import Foundation
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

public struct HTTPResponse: Sendable {
    public let statusCode: Int
    /// Header names lower-cased.
    public let headers: [String: String]
    public let body: Data

    public init(statusCode: Int, headers: [String: String], body: Data) {
        self.statusCode = statusCode
        self.headers = headers
        self.body = body
    }
}

public protocol HTTPTransport: Sendable {
    func send(_ request: URLRequest) async throws -> HTTPResponse
}

public struct URLSessionTransport: HTTPTransport {
    private let session: URLSession

    public init(session: URLSession = URLSessionTransport.makeSession()) {
        self.session = session
    }

    public static func makeSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        #if !canImport(FoundationNetworking)
        // `waitsForConnectivity` is get-only in swift-corelibs-foundation (Windows/Linux).
        configuration.waitsForConnectivity = false
        #endif
        configuration.requestCachePolicy = .reloadIgnoringLocalCacheData
        return URLSession(configuration: configuration)
    }

    public func send(_ request: URLRequest) async throws -> HTTPResponse {
        try await withCheckedThrowingContinuation { continuation in
            let task = session.dataTask(with: request) { data, response, error in
                if let error {
                    continuation.resume(throwing: error)
                    return
                }
                guard let http = response as? HTTPURLResponse else {
                    continuation.resume(throwing: URLError(.badServerResponse))
                    return
                }
                var headers: [String: String] = [:]
                for (key, value) in http.allHeaderFields {
                    if let name = key as? String, let text = value as? String {
                        headers[name.lowercased()] = text
                    }
                }
                continuation.resume(returning: HTTPResponse(statusCode: http.statusCode, headers: headers, body: data ?? Data()))
            }
            task.resume()
        }
    }
}

/// RFC 1123 dates as used by `Retry-After`.
public enum HTTPDate {
    private static let formatter: DateFormatter = {
        let f = DateFormatter()
        f.locale = Locale(identifier: "en_US_POSIX")
        f.timeZone = TimeZone(secondsFromGMT: 0)
        f.dateFormat = "EEE, dd MMM yyyy HH:mm:ss zzz"
        return f
    }()

    public static func parse(_ text: String) -> Date? {
        formatter.date(from: text.trimmingCharacters(in: .whitespaces))
    }
}
