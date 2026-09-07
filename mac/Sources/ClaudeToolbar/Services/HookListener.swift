import Foundation
import Network

/// Minimal loopback HTTP/1.1 server for Claude Code hooks.
@MainActor
final class HookListener {
    static let maxBodyBytes = 64 * 1024

    private var listener: NWListener?
    private let queue = DispatchQueue(label: "io.github.ahm-adsaad.ClaudeToolbar.hooks")

    private(set) var port: UInt16?
    private(set) var error: String?
    var onHook: ((String) -> Void)?
    var onStateChanged: (() -> Void)?

    var isListening: Bool { listener != nil && error == nil }

    func start(port: UInt16) {
        stop()
        self.port = port
        guard let endpointPort = NWEndpoint.Port(rawValue: port) else {
            error = "Port \(port) is not usable"
            Log.error("Hook listener could not start: port \(port) is not usable")
            onStateChanged?()
            return
        }
        let parameters = NWParameters.tcp
        parameters.allowLocalEndpointReuse = true
        // Bound to the loopback address only: nothing outside this Mac can reach the hook endpoint.
        parameters.requiredLocalEndpoint = NWEndpoint.hostPort(host: .ipv4(.loopback), port: endpointPort)
        let queue = self.queue
        do {
            let listener = try NWListener(using: parameters)
            self.listener = listener
            listener.stateUpdateHandler = { [weak self] state in
                Task { @MainActor in
                    guard let self else { return }
                    switch state {
                    case .ready:
                        self.error = nil
                        Log.info("Hook listener on http://127.0.0.1:\(port)/hook")
                    case .failed(let failure):
                        self.error = "Port \(port) is in use"
                        Log.error("Hook listener failed: \(failure)")
                    default:
                        break
                    }
                    self.onStateChanged?()
                }
            }
            listener.newConnectionHandler = { [weak self] connection in
                guard Self.isLoopback(connection.endpoint) else {
                    connection.cancel()
                    return
                }
                _ = HookConnection(connection, queue: queue) { body in
                    Task { @MainActor in self?.onHook?(body) }
                }
            }
            listener.start(queue: queue)
        } catch {
            listener = nil
            self.error = "Port \(port) is in use"
            Log.error("Hook listener could not start: \(error)")
            onStateChanged?()
        }
    }

    func stop() {
        listener?.cancel()
        listener = nil
        port = nil
        error = nil
    }

    /// Belt and braces next to `requiredLocalEndpoint`: a remote that is not 127.0.0.1 or ::1 is dropped unread.
    nonisolated static func isLoopback(_ endpoint: NWEndpoint) -> Bool {
        guard case .hostPort(let host, _) = endpoint else { return false }
        switch host {
        case .ipv4(let address): return address.isLoopback
        case .ipv6(let address): return address.isLoopback
        default: return false
        }
    }
}

/// One inbound request. Keeps itself alive through the connection callbacks until it responds or fails.
final class HookConnection: @unchecked Sendable {
    private let connection: NWConnection
    private let deliver: @Sendable (String) -> Void
    private var buffer = Data()
    private var timeoutWork: DispatchWorkItem?
    private var responded = false

    init(_ connection: NWConnection, queue: DispatchQueue, deliver: @escaping @Sendable (String) -> Void) {
        self.connection = connection
        self.deliver = deliver
        connection.start(queue: queue)
        let timeout = DispatchWorkItem { [self] in self.respond(status: "408 Request Timeout", body: "") }
        timeoutWork = timeout
        queue.asyncAfter(deadline: .now() + 2, execute: timeout)
        receive()
    }

    private func receive() {
        connection.receive(minimumIncompleteLength: 1, maximumLength: 16 * 1024) { [self] data, _, isComplete, error in
            if let data { buffer.append(data) }
            if error != nil {
                timeoutWork?.cancel()
                connection.cancel()
                return
            }
            if tryHandle() { return }
            if isComplete || buffer.count > HookListener.maxBodyBytes + 8192 {
                respond(status: "400 Bad Request", body: "")
                return
            }
            receive()
        }
    }

    /// True when the request was fully handled (a response was sent).
    private func tryHandle() -> Bool {
        guard let headerEnd = buffer.range(of: Data("\r\n\r\n".utf8)) else { return false }
        let head = String(decoding: buffer[buffer.startIndex..<headerEnd.lowerBound], as: UTF8.self)
        let lines = head.components(separatedBy: "\r\n")
        let parts = lines[0].split(separator: " ")
        guard parts.count >= 2 else {
            respond(status: "400 Bad Request", body: "")
            return true
        }
        let method = String(parts[0])
        let path = String(parts[1])
        var contentLength = 0
        for line in lines.dropFirst() where line.lowercased().hasPrefix("content-length:") {
            contentLength = Int(line.dropFirst("content-length:".count).trimmingCharacters(in: .whitespaces)) ?? 0
        }
        if contentLength < 0 {
            respond(status: "400 Bad Request", body: "")
            return true
        }
        if contentLength > HookListener.maxBodyBytes {
            respond(status: "413 Payload Too Large", body: "")
            return true
        }
        let bodyStart = headerEnd.upperBound
        guard buffer.endIndex - bodyStart >= contentLength else { return false }
        let body = String(decoding: buffer[bodyStart..<(bodyStart + contentLength)], as: UTF8.self)
        switch (method, path) {
        case ("POST", "/hook"):
            deliver(body)
            respond(status: "200 OK", body: "")
        case ("GET", "/health"):
            respond(status: "200 OK", body: "ClaudeToolbar \(AppInfo.version)")
        default:
            respond(status: "404 Not Found", body: "")
        }
        return true
    }

    private func respond(status: String, body: String) {
        guard !responded else { return }
        responded = true
        timeoutWork?.cancel()
        let payload = "HTTP/1.1 \(status)\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: \(body.utf8.count)\r\nConnection: close\r\n\r\n" + body
        let connection = self.connection
        connection.send(content: Data(payload.utf8), completion: .contentProcessed { _ in connection.cancel() })
    }
}
