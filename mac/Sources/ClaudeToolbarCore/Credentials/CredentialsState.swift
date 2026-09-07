import Foundation

public enum CredentialsState: Equatable, Sendable, CustomStringConvertible {
    case missing(source: String)
    case invalid(source: String, reason: String)
    case expired(source: String, expiresAt: Date, subscriptionType: String?)
    case valid(source: String, accessToken: String, expiresAt: Date, subscriptionType: String?)

    public var source: String {
        switch self {
        case .missing(let s), .invalid(let s, _), .expired(let s, _, _), .valid(let s, _, _, _):
            return s
        }
    }

    public var isMissing: Bool {
        if case .missing = self { return true }
        return false
    }

    public var subscriptionType: String? {
        switch self {
        case .expired(_, _, let sub), .valid(_, _, _, let sub): return sub
        default: return nil
        }
    }

    public var expiresAt: Date? {
        switch self {
        case .expired(_, let e, _), .valid(_, _, let e, _): return e
        default: return nil
        }
    }

    /// Never includes the token.
    public var description: String {
        switch self {
        case .missing(let s): return "missing (\(s))"
        case .invalid(let s, let reason): return "invalid (\(s)): \(reason)"
        case .expired(let s, let e, _): return "expired (\(s)) at \(e)"
        case .valid(let s, _, let e, _): return "valid (\(s)) until \(e)"
        }
    }
}
