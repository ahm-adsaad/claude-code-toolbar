import Foundation
import Network

/// Calls back on the main actor when the network becomes reachable after having been unreachable.
@MainActor
final class NetworkObserver {
    private let monitor = NWPathMonitor()
    private var wasSatisfied = true

    init(onReachable: @escaping @MainActor () -> Void) {
        monitor.pathUpdateHandler = { [weak self] path in
            let satisfied = path.status == .satisfied
            Task { @MainActor in
                guard let self else { return }
                if satisfied && !self.wasSatisfied { onReachable() }
                self.wasSatisfied = satisfied
            }
        }
        monitor.start(queue: DispatchQueue(label: "io.github.ahm-adsaad.ClaudeToolbar.network"))
    }

    deinit {
        monitor.cancel()
    }
}
