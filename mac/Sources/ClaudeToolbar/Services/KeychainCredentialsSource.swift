import Foundation
import ClaudeToolbarCore

/// Reads the OAuth bundle Claude Code stores in the login Keychain, through the same `security` tool Claude Code uses
/// (that tool is on the item's access list, so no permission dialog appears). Never writes.
final class KeychainCredentialsSource: CredentialsSource, @unchecked Sendable {
    static let service = "Claude Code-credentials"
    static let securityTool = "/usr/bin/security"
    static let itemNotFoundStatus: Int32 = 44
    static let timeout: TimeInterval = 20

    let sourceName = "Keychain"
    private let clock: any ClockSource

    init(clock: any ClockSource) {
        self.clock = clock
    }

    func read() -> CredentialsState {
        let result = ProcessRunner.run(Self.securityTool,
                                       arguments: ["find-generic-password", "-s", Self.service, "-w"],
                                       timeout: Self.timeout)
        if result.timedOut {
            return .invalid(source: sourceName, reason: "Keychain access timed out")
        }
        if result.status == Self.itemNotFoundStatus {
            return .missing(source: sourceName)
        }
        guard result.status == 0 else {
            return .invalid(source: sourceName, reason: "security exited with status \(result.status)")
        }
        return CredentialsPayloadParser.parse(result.stdout, source: sourceName, now: clock.now)
    }
}
