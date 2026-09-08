import Foundation
import CProcInfo

/// Finds the process holding the client end of a loopback connection.
enum PeerProcess {
    static func pid(clientPort: UInt16, listenerPort: UInt16) -> pid_t? {
        let pid = cproc_pid_for_tcp(clientPort, listenerPort)
        return pid > 0 ? pid : nil
    }
}
