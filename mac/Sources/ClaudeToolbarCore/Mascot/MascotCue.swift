/// Why the mascot should wave. Ordered by strength so the strongest cue in a batch wins.
public enum MascotCue: Int, Comparable, Sendable {
    case greeting
    case hover
    case warn
    case crit
    case finished
    case failed
    case attention

    public static func < (lhs: MascotCue, rhs: MascotCue) -> Bool { lhs.rawValue < rhs.rawValue }
}
