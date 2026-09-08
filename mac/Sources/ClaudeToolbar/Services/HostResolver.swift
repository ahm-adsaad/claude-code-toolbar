import Foundation
import ClaudeToolbarCore
import CProcInfo

/// Maps a hook request's client port to the window-owning host of that Claude Code process.
/// Runs on the listener's queue while the connection is open; a session costs one lookup. Thread-safe.
final class HostResolver: @unchecked Sendable {
    private struct Key: Hashable {
        let pid: Int
        let start: Int64   // a reused pid gets a fresh lookup
    }

    private let lock = NSLock()
    private var cache: [Key: SessionHost?] = [:]

    func resolve(clientPort: UInt16, listenerPort: UInt16) -> SessionHost? {
        guard let claudePid = PeerProcess.pid(clientPort: clientPort, listenerPort: listenerPort) else { return nil }
        let key = Key(pid: Int(claudePid), start: cproc_start_time(claudePid))
        lock.lock()
        if let cached = cache[key] {
            lock.unlock()
            return cached
        }
        lock.unlock()

        let result = HostChain.resolve(startPid: Int(claudePid), table: ProcessTable.chain(from: claudePid), catalog: KnownHosts.mac)
        let host = result.map { SessionHost(pid: $0.pid, name: $0.displayName, resolvedAt: Date()) }
        lock.lock()
        if cache.count > 256 { cache.removeAll() }
        cache[key] = host
        lock.unlock()
        Log.info(host.map { "Session host: pid \(claudePid) → \($0.name) (pid \($0.pid))" }
                 ?? "Session host: pid \(claudePid) has no window-owning ancestor")
        return host
    }
}
