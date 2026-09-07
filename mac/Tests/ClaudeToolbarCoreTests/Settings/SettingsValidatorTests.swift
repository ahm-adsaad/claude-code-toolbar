import XCTest
@testable import ClaudeToolbarCore

final class SettingsValidatorTests: XCTestCase {
    func testColourValidation() {
        XCTAssertTrue(SettingsValidator.isValidColor("#FF3FB950"))
        XCTAssertTrue(SettingsValidator.isValidColor("#ff3fb950"))
        XCTAssertFalse(SettingsValidator.isValidColor("#3FB950"))
        XCTAssertFalse(SettingsValidator.isValidColor("FF3FB950"))
        XCTAssertFalse(SettingsValidator.isValidColor("#GG3FB950"))
        XCTAssertFalse(SettingsValidator.isValidColor(nil))
    }

    func testColourNormalisation() {
        XCTAssertEqual(SettingsValidator.normalizeColor("#ff3fb950", fallback: "#FF000000"), "#FF3FB950")
        XCTAssertEqual(SettingsValidator.normalizeColor("#3fb950", fallback: "#FF000000"), "#FF3FB950")
        XCTAssertEqual(SettingsValidator.normalizeColor("blue", fallback: "#FF000000"), "#FF000000")
        XCTAssertEqual(SettingsValidator.normalizeColor(nil, fallback: "#FF000000"), "#FF000000")
    }

    func testClampsRanges() {
        var s = AppSettings.createDefault()
        s.appearance.warnThreshold = 0
        s.appearance.critThreshold = 150
        s.rows.barWidth = 500
        s.behavior.refreshIntervalSeconds = 5
        s.version = 9
        let n = SettingsValidator.normalize(s)
        XCTAssertEqual(n.appearance.warnThreshold, 1)
        XCTAssertEqual(n.appearance.critThreshold, 100)
        XCTAssertEqual(n.rows.barWidth, 80)
        XCTAssertEqual(n.behavior.refreshIntervalSeconds, 30)
        XCTAssertEqual(n.version, 1)

        s.rows.barWidth = 1
        s.behavior.refreshIntervalSeconds = 10_000
        let m = SettingsValidator.normalize(s)
        XCTAssertEqual(m.rows.barWidth, 24)
        XCTAssertEqual(m.behavior.refreshIntervalSeconds, 300)
    }

    func testWarnAtOrAboveCritResetsBoth() {
        var s = AppSettings.createDefault()
        s.appearance.warnThreshold = 90
        s.appearance.critThreshold = 80
        let n = SettingsValidator.normalize(s)
        XCTAssertEqual(n.appearance.warnThreshold, 70)
        XCTAssertEqual(n.appearance.critThreshold, 90)

        s.appearance.warnThreshold = 50
        s.appearance.critThreshold = 50
        XCTAssertEqual(SettingsValidator.normalize(s).appearance.warnThreshold, 70)
    }

    func testBadColoursFallBackToDefaults() {
        var s = AppSettings.createDefault()
        s.appearance.barOk = "green"
        s.appearance.barCrit = "#f85149"
        let n = SettingsValidator.normalize(s)
        XCTAssertEqual(n.appearance.barOk, "#FF3FB950")
        XCTAssertEqual(n.appearance.barCrit, "#FFF85149")
    }

    func testPresetNameNormalisation() {
        var s = AppSettings.createDefault()
        s.appearance.preset = "  Mono "
        XCTAssertEqual(SettingsValidator.normalize(s).appearance.preset, "mono")
        s.appearance.preset = ""
        XCTAssertEqual(SettingsValidator.normalize(s).appearance.preset, "custom")
        s.appearance.preset = "neon"
        XCTAssertEqual(SettingsValidator.normalize(s).appearance.preset, "custom")
    }
}
