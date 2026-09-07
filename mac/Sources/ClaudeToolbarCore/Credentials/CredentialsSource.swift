public protocol CredentialsSource: Sendable {
    /// Human-readable origin shown in the Account section ("Keychain" or a file path).
    var sourceName: String { get }
    /// Synchronous; may block for a subprocess. Callers run it off the main thread.
    func read() -> CredentialsState
}
