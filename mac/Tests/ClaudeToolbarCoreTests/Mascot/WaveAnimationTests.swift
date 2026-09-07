import XCTest
@testable import ClaudeToolbarCore

final class WaveAnimationTests: XCTestCase {
    func testRestsOutsideTheWave() {
        for p in [0.0, 1.0, -0.5, 1.5] {
            XCTAssertEqual(WaveAnimation.armAngle(progress: p), WaveAnimation.restAngle, "progress \(p)")
        }
    }

    func testRaisedAtTheMidpoint() {
        XCTAssertEqual(WaveAnimation.armAngle(progress: 0.5), WaveAnimation.raisedAngle, accuracy: 1e-6)
    }

    func testRampRisesMonotonically() {
        var previous = WaveAnimation.armAngle(progress: 0)
        var p = 0.01
        while p <= WaveAnimation.rampFraction + 1e-9 {
            let angle = WaveAnimation.armAngle(progress: p)
            XCTAssertGreaterThan(angle, previous, "angle at \(p)")
            previous = angle
            p += 0.01
        }
        XCTAssertEqual(WaveAnimation.armAngle(progress: WaveAnimation.rampFraction), WaveAnimation.raisedAngle, accuracy: 1e-6)
    }

    func testRampsAreSymmetric() {
        XCTAssertEqual(WaveAnimation.armAngle(progress: 0.075), WaveAnimation.armAngle(progress: 0.925), accuracy: 1e-9)
    }

    func testWobbleStaysWithinBounds() {
        var p = WaveAnimation.rampFraction
        while p <= 1 - WaveAnimation.rampFraction {
            let angle = WaveAnimation.armAngle(progress: p)
            XCTAssertGreaterThanOrEqual(angle, WaveAnimation.raisedAngle - WaveAnimation.wobbleDegrees - 1e-9)
            XCTAssertLessThanOrEqual(angle, WaveAnimation.raisedAngle + WaveAnimation.wobbleDegrees + 1e-9)
            p += 0.005
        }
    }

    func testProgressClampsToTheDuration() {
        let start = Date(timeIntervalSince1970: 1_788_782_400)
        XCTAssertEqual(WaveAnimation.progress(startedAt: start, now: start.addingTimeInterval(-1)), 0)
        // Date arithmetic at Unix-epoch magnitude (~1.79e9 s) loses sub-microsecond precision in Double
        // on every platform, so this needs a looser tolerance than the other exact comparisons here.
        XCTAssertEqual(WaveAnimation.progress(startedAt: start, now: start.addingTimeInterval(0.6)), 0.5, accuracy: 1e-6)
        XCTAssertEqual(WaveAnimation.progress(startedAt: start, now: start.addingTimeInterval(5)), 1)
        XCTAssertEqual(WaveAnimation.duration, 1.2)
        XCTAssertEqual(WaveAnimation.framesPerSecond, 15)
    }
}
