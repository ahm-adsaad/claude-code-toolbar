import XCTest
@testable import ClaudeToolbarCore

final class SettingsStoreTests: XCTestCase {
    private var directory: URL!
    private var path: String { directory.appendingPathComponent("nested").appendingPathComponent("settings.json").path }

    override func setUpWithError() throws {
        directory = FileManager.default.temporaryDirectory.appendingPathComponent("ct-settings-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: directory)
    }

    func testLoadWithoutFileGivesDefaultsAndDoesNotCreateIt() {
        let store = SettingsStore(path: path)
        XCTAssertEqual(store.load(), SettingsValidator.normalize(AppSettings.createDefault()))
        XCTAssertFalse(FileManager.default.fileExists(atPath: path))
    }

    func testSaveCreatesDirectoriesAndRoundTrips() throws {
        let store = SettingsStore(path: path)
        var s = AppSettings.createDefault()
        s.rows.barWidth = 64
        s.behavior.launchAtLogin = false
        try store.save(s)
        XCTAssertTrue(FileManager.default.fileExists(atPath: path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: path + ".tmp"))
        let loaded = store.load()
        XCTAssertEqual(loaded.rows.barWidth, 64)
        XCTAssertFalse(loaded.behavior.launchAtLogin)
    }

    func testSaveNormalises() throws {
        let store = SettingsStore(path: path)
        var s = AppSettings.createDefault()
        s.behavior.refreshIntervalSeconds = 1
        try store.save(s)
        XCTAssertEqual(store.load().behavior.refreshIntervalSeconds, 30)
    }

    func testUnreadableFileIsBackedUpAndReplaced() throws {
        try FileManager.default.createDirectory(at: URL(fileURLWithPath: path).deletingLastPathComponent(), withIntermediateDirectories: true)
        _ = FileManager.default.createFile(atPath: path, contents: Data("{ broken".utf8))
        let store = SettingsStore(path: path)
        let loaded = store.load()
        XCTAssertEqual(loaded, SettingsValidator.normalize(AppSettings.createDefault()))
        XCTAssertTrue(FileManager.default.fileExists(atPath: path + ".bad"))
        XCTAssertEqual(try String(contentsOfFile: path + ".bad", encoding: .utf8), "{ broken")
        XCTAssertNoThrow(try SettingsJSON.decode(try String(contentsOfFile: path, encoding: .utf8)))
    }
}
