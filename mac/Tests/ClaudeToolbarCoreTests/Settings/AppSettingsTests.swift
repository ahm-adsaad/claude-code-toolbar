import XCTest
@testable import ClaudeToolbarCore

final class AppSettingsTests: XCTestCase {
    func testDefaults() {
        let s = AppSettings.createDefault()
        XCTAssertEqual(s.version, 1)
        XCTAssertEqual(s.appearance.preset, "default")
        XCTAssertEqual(s.appearance.barOk, "#FF3FB950")
        XCTAssertEqual(s.appearance.barWarn, "#FFD29922")
        XCTAssertEqual(s.appearance.barCrit, "#FFF85149")
        XCTAssertEqual(s.appearance.warnThreshold, 70)
        XCTAssertEqual(s.appearance.critThreshold, 90)
        XCTAssertTrue(s.rows.showFiveHour)
        XCTAssertTrue(s.rows.showSevenDay)
        XCTAssertFalse(s.rows.showSevenDayOpus)
        XCTAssertFalse(s.rows.showSevenDaySonnet)
        XCTAssertTrue(s.rows.showLabel)
        XCTAssertTrue(s.rows.showBar)
        XCTAssertTrue(s.rows.showPercent)
        XCTAssertFalse(s.rows.showTime)
        XCTAssertEqual(s.rows.barWidth, 40)
        XCTAssertEqual(s.behavior.refreshIntervalSeconds, 60)
        XCTAssertTrue(s.behavior.launchAtLogin)
    }

    func testRoundTrip() throws {
        var s = AppSettings.createDefault()
        s.appearance.barOk = "#FF112233"
        s.rows.showSevenDayOpus = true
        s.rows.barWidth = 56
        s.behavior.refreshIntervalSeconds = 120
        s.behavior.launchAtLogin = false
        let json = try SettingsJSON.encode(s)
        XCTAssertTrue(json.contains("\"barOk\" : \"#FF112233\"") || json.contains("\"barOk\":\"#FF112233\""))
        let back = try SettingsJSON.decode(json)
        XCTAssertEqual(back, s)
    }

    func testMissingFieldsTakeDefaultsAndUnknownFieldsAreIgnored() throws {
        let json = ##"{ "version": 1, "appearance": { "barOk": "#FF000000", "mystery": 5 }, "rows": {}, "somethingElse": true }"##
        let s = try SettingsJSON.decode(json)
        XCTAssertEqual(s.appearance.barOk, "#FF000000")
        XCTAssertEqual(s.appearance.barWarn, "#FFD29922")
        XCTAssertTrue(s.rows.showFiveHour)
        XCTAssertEqual(s.behavior.refreshIntervalSeconds, 60)
    }

    func testEmptyObjectIsAllDefaults() throws {
        XCTAssertEqual(try SettingsJSON.decode("{}"), AppSettings.createDefault())
    }

    func testWrongTypeThrows() {
        XCTAssertThrowsError(try SettingsJSON.decode(#"{ "behavior": { "refreshIntervalSeconds": "fast" } }"#))
        XCTAssertThrowsError(try SettingsJSON.decode("[]"))
    }
}
