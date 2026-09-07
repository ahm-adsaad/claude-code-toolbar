import XCTest
@testable import ClaudeToolbarCore

final class MascotModeTests: XCTestCase {
    func testNormalize() {
        XCTAssertEqual(MascotMode.normalize("full"), "full")
        XCTAssertEqual(MascotMode.normalize(" Hover "), "hover")
        XCTAssertEqual(MascotMode.normalize("OFF"), "off")
        XCTAssertEqual(MascotMode.normalize("neon"), "full")
        XCTAssertEqual(MascotMode.normalize(""), "full")
        XCTAssertEqual(MascotMode.normalize(nil), "full")
        XCTAssertEqual(MascotMode.all, ["full", "hover", "off"])
    }

    func testDefaultRoundTripAndValidation() throws {
        XCTAssertEqual(AppSettings.createDefault().behavior.mascot, "full")
        var s = AppSettings.createDefault()
        s.behavior.mascot = "off"
        let json = try SettingsJSON.encode(s)
        XCTAssertTrue(json.contains("\"mascot\""))
        XCTAssertEqual(try SettingsJSON.decode(json).behavior.mascot, "off")
        XCTAssertEqual(try SettingsJSON.decode(#"{ "behavior": {} }"#).behavior.mascot, "full")
        s.behavior.mascot = "HOVER"
        XCTAssertEqual(SettingsValidator.normalize(s).behavior.mascot, "hover")
        s.behavior.mascot = "sparkles"
        XCTAssertEqual(SettingsValidator.normalize(s).behavior.mascot, "full")
    }
}
