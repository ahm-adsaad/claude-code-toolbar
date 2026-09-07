import Foundation
import ClaudeToolbarCore
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

struct FakeTransportError: Error, LocalizedError {
    var errorDescription: String? { "socket closed" }
}

final class FakeTransport: HTTPTransport, @unchecked Sendable {
    private let lock = NSLock()
    private var responses: [Result<HTTPResponse, Error>] = []
    private var requests: [URLRequest] = []

    init(_ response: HTTPResponse) { responses = [.success(response)] }
    init(error: Error) { responses = [.failure(error)] }
    init(responses: [Result<HTTPResponse, Error>]) { self.responses = responses }

    var lastRequest: URLRequest? {
        lock.lock(); defer { lock.unlock() }
        return requests.last
    }

    var requestCount: Int {
        lock.lock(); defer { lock.unlock() }
        return requests.count
    }

    func send(_ request: URLRequest) async throws -> HTTPResponse {
        let next = record(request)
        return try next.get()
    }

    /// The lock lives here because `NSLock.lock()` is unavailable from an async context.
    private func record(_ request: URLRequest) -> Result<HTTPResponse, Error> {
        lock.lock(); defer { lock.unlock() }
        requests.append(request)
        guard !responses.isEmpty else { return .failure(FakeTransportError()) }
        return responses.count == 1 ? responses[0] : responses.removeFirst()
    }

    static func response(_ status: Int, body: String = "", headers: [String: String] = [:]) -> HTTPResponse {
        HTTPResponse(statusCode: status, headers: headers, body: Data(body.utf8))
    }
}
