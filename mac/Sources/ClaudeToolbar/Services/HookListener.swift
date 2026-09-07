import Foundation
import Network

/// Minimal loopback HTTP/1.1 server for Claude Code hooks.
@MainActor
final class HookListener {
    /// Read from the connection queue, so it must not be main-actor isolated like the rest of the class.
    nonisolated static let maxBodyBytes = 64 * 1024

    private var listener: NWListener?
    private let queue = DispatchQueue(label: "io.github.ahm-adsaad.ClaudeToolbar.hooks")
    /// Bumped on every start and stop: a callback already in flight when a listener is cancelled
    /// carries the old value and is ignored instead of writing a stale error over the new listener.
    private var generation = 0

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
        generation += 1
        let generation = self.generation
        do {
            let listener = try NWListener(using: parameters)
            self.listener = listener
            listener.stateUpdateHandler = { [weak self] state in
                Task { @MainActor in
                    guard let self, generation == self.generation else { return }
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
                // Bound once here: capturing the weak variable itself inside the delivery
                // closure would hand concurrent code a reference to a mutable capture.
                guard let self, Self.isLoopback(connection.endpoint) else {
                    connection.cancel()
                    return
                }
                _ = HookConnection(connection, queue: queue) { body in
                    // The main queue keeps the events in the order they arrived; independent
                    // tasks would not, and a Stop overtaking its Notification would leave the
                    // session in the wrong state.
                    DispatchQueue.main.async {
                        MainActor.assumeIsolated {
                            guard generation == self.generation else { return }
                            self.onHook?(body)
                        }
                    }
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
        // Drop the handlers before cancelling: a .failed state posted just before the cancel
        // would otherwise land after the replacement listener is ready and mark it broken.
        listener?.stateUpdateHandler = nil
        listener?.newConnectionHandler = nil
        listener?.cancel()
        listener = nil
        generation += 1
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
        // Weak: a strong capture here would be a cycle through timeoutWork that cancel() does
        // not break. The pending receive keeps this object alive for as long as the read lasts.
        let timeout = DispatchWorkItem { [weak self] in self?.respond(status: "400 Bad Request", body: "") }
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
        timeoutWork = nil
    }
}
