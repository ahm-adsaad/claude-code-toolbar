/// Tries each source in order and returns the first result that is not `.missing`.
public struct CompositeCredentialsSource: CredentialsSource {
    private let sources: [any CredentialsSource]

    public init(_ sources: [any CredentialsSource]) {
        precondition(!sources.isEmpty, "at least one credentials source is required")
        self.sources = sources
    }

    public var sourceName: String { sources.map(\.sourceName).joined(separator: ", ") }

    public func read() -> CredentialsState {
        var firstMissing: CredentialsState?
        for source in sources {
            let state = source.read()
            if state.isMissing {
                if firstMissing == nil { firstMissing = state }
                continue
            }
            return state
        }
        return firstMissing ?? .missing(source: sourceName)
    }
}
