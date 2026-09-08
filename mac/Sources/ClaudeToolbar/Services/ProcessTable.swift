import AppKit
import ClaudeToolbarCore
import CProcInfo

/// The chain of processes above a pid. Names are bundle ids for apps and short process names otherwise;
/// a process "has a window" when it is a regular (Dock-visible) application.
enum ProcessTable {
    static func chain(from startPid: pid_t) -> [Int: ProcessRecord] {
        var table: [Int: ProcessRecord] = [:]
        var pid = startPid
        var depth = 0
        while pid > 0 && depth <= HostChain.maxDepth && table[Int(pid)] == nil {
            let parent = cproc_parent(pid)
            if parent < 0 { break }
            let app = NSRunningApplication(processIdentifier: pid)
            table[Int(pid)] = ProcessRecord(
                pid: Int(pid),
                parentPid: Int(parent),
                name: app?.bundleIdentifier ?? shortName(pid),
                startTime: Date(timeIntervalSince1970: TimeInterval(cproc_start_time(pid))),
                hasWindow: app?.activationPolicy == .regular)
            pid = parent
            depth += 1
        }
        return table
    }

    static func shortName(_ pid: pid_t) -> String {
        var buffer = [CChar](repeating: 0, count: 64)
        let length = cproc_name(pid, &buffer, Int32(buffer.count))
        return length > 0 ? String(cString: buffer) : "pid \(pid)"
    }
}
