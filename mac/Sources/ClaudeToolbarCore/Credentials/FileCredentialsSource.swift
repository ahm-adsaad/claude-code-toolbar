import Foundation

public struct FileCredentialsSource: CredentialsSource {
    public let path: String
    private let clock: any ClockSource

    public init(path: String, clock: any ClockSource) {
        self.path = path
        self.clock = clock
    }

    public var sourceName: String { path }

    public func read() -> CredentialsState {
        guard FileManager.default.fileExists(atPath: path) else { return .missing(source: path) }
        let text: String
        do {
            text = try String(contentsOfFile: path, encoding: .utf8)
        } catch {
            return .invalid(source: path, reason: error.localizedDescription)
        }
        return CredentialsPayloadParser.parse(text, source: path, now: clock.now)
    }
}
