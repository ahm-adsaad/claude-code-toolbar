import XCTest
@testable import ClaudeToolbarCore

final class PresetsTests: XCTestCase {
    func testNames() {
        XCTAssertEqual(Presets.names, ["default", "claude", "mono"])
        XCTAssertEqual(Presets.defaultName, "default")
        XCTAssertEqual(Presets.custom, "custom")
    }

    func testApplySetsColoursAndName() {
        var a = AppearanceSettings()
        XCTAssertTrue(Presets.apply(" Claude ", to: &a))
        XCTAssertEqual(a.preset, "claude")
        XCTAssertEqual(a.barOk, "#FFD97757")
        XCTAssertEqual(a.barWarn, "#FFE8A34F")
        XCTAssertEqual(a.barCrit, "#FFE5484D")
        XCTAssertEqual(a.warnThreshold, 70, "thresholds are not part of a preset")
    }

    func testUnknownPresetIsRejected() {
        var a = AppearanceSettings()
        XCTAssertFalse(Presets.apply("neon", to: &a))
        XCTAssertEqual(a, AppearanceSettings())
    }

    func testMatching() {
        var a = AppearanceSettings()
        XCTAssertEqual(Presets.matching(a), "default")
        Presets.apply("mono", to: &a)
        XCTAssertEqual(Presets.matching(a), "mono")
        a.barOk = "#FF010203"
        XCTAssertEqual(Presets.matching(a), "custom")
    }
}
