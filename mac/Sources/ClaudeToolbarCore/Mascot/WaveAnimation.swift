import Foundation

/// Timing of one wave. Pure functions so the host and the tests agree on every frame.
public enum WaveAnimation {
    public static let duration: TimeInterval = 1.2
    public static let framesPerSecond = 15
    public static let restAngle: Double = -60
    public static let raisedAngle: Double = 50
    public static let wobbleDegrees: Double = 25
    public static let wobbles = 3
    public static let rampFraction = 0.15

    /// Arm angle in degrees above horizontal for a wave at `progress` (0…1).
    public static func armAngle(progress: Double) -> Double {
        if progress <= 0 || progress >= 1 { return restAngle }

        var envelope: Double
        var wobble = 0.0
        if progress < rampFraction {
            envelope = progress / rampFraction
        } else if progress > 1 - rampFraction {
            envelope = (1 - progress) / rampFraction
        } else {
            envelope = 1
            let middle = (progress - rampFraction) / (1 - 2 * rampFraction)
            wobble = wobbleDegrees * sin(2 * .pi * Double(wobbles) * middle)
        }
        return restAngle + envelope * (raisedAngle - restAngle) + wobble
    }

    public static func progress(startedAt: Date, now: Date) -> Double {
        min(max(now.timeIntervalSince(startedAt) / duration, 0), 1)
    }
}
