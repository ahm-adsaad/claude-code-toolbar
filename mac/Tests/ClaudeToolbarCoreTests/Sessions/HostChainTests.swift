import XCTest
@testable import ClaudeToolbarCore

final class HostChainTests: XCTestCase {
    private let t0 = Date(timeIntervalSince1970: 1_788_868_800)

    // startOffset: larger = started later. Children start after their parents.
    private func p(_ pid: Int, _ parent: Int, _ name: String, window: Bool = false, startOffset: TimeInterval = 0) -> ProcessRecord {
        ProcessRecord(pid: pid, parentPid: parent, name: name, startTime: t0.addingTimeInterval(startOffset), hasWindow: window)
    }

    private func table(_ records: ProcessRecord...) -> [Int: ProcessRecord] {
        Dictionary(uniqueKeysWithValues: records.map { ($0.pid, $0) })
    }

    func testFindsTerminalThreeLevelsUp() {
        let t = table(
            p(100, 90, "node", startOffset: 30),
            p(90, 80, "zsh", startOffset: 20),
            p(80, 70, "login", startOffset: 10),
            p(70, 1, "com.apple.Terminal", window: true))
        XCTAssertEqual(HostChain.resolve(startPid: 100, table: t, catalog: KnownHosts.mac),
                       HostChainResult(pid: 70, displayName: "Terminal", known: true))
    }

    func testTheClaudeCliIsNotTheDesktopApp() {
        let t = table(p(100, 90, "claude", startOffset: 10), p(90, 1, "windowsterminal", window: true))
        XCTAssertEqual(HostChain.resolve(startPid: 100, table: t, catalog: KnownHosts.windows)?.displayName, "Windows Terminal")
    }

    func testTheDesktopAppCountsWhenItOwnsAWindow() {
        let t = table(p(100, 50, "claude", startOffset: 10), p(50, 1, "com.anthropic.claudefordesktop", window: true))
        XCTAssertEqual(HostChain.resolve(startPid: 100, table: t, catalog: KnownHosts.mac),
                       HostChainResult(pid: 50, displayName: "Claude", known: true))
    }

    func testVsCodeHelperWithoutAWindowIsSkippedForItsMainProcess() {
        let t = table(
            p(100, 90, "node", startOffset: 30),
            p(90, 80, "zsh", startOffset: 20),
            p(80, 70, "com.microsoft.VSCode.helper.plugin", startOffset: 10),
            p(70, 1, "com.microsoft.VSCode", window: true))
        XCTAssertEqual(HostChain.resolve(startPid: 100, table: t, catalog: KnownHosts.mac),
                       HostChainResult(pid: 70, displayName: "VS Code", known: true))
    }

    func testAPlainConsoleIsTheShellThatOwnsTheWindow() {
        let t = table(p(100, 90, "claude", startOffset: 10), p(90, 1, "pwsh", window: true))
        XCTAssertEqual(HostChain.resolve(startPid: 100, table: t, catalog: KnownHosts.windows),
                       HostChainResult(pid: 90, displayName: "Console", known: true))
    }

    func testUnknownHostFallsBackToTheFirstWindowedAncestor() {
        let t = table(p(100, 90, "node", startOffset: 10), p(90, 80, "zsh", startOffset: 5), p(80, 1, "com.example.myterm", window: true))
        XCTAssertEqual(HostChain.resolve(startPid: 100, table: t, catalog: KnownHosts.mac),
                       HostChainResult(pid: 80, displayName: "com.example.myterm", known: false))
    }

    func testAKnownHostBeatsAnEarlierUnknownWindowedAncestor() {
        let t = table(
            p(100, 90, "node", startOffset: 20),
            p(90, 80, "com.example.helper", window: true, startOffset: 10),
            p(80, 1, "com.googlecode.iterm2", window: true))
        XCTAssertEqual(HostChain.resolve(startPid: 100, table: t, catalog: KnownHosts.mac)?.displayName, "iTerm2")
    }

    func testStopsAtTheShellWithoutPickingIt() {
        let t = table(p(100, 90, "node", startOffset: 10), p(90, 1, "com.apple.finder", window: true))
        XCTAssertNil(HostChain.resolve(startPid: 100, table: t, catalog: KnownHosts.mac))
        let w = table(p(100, 90, "claude", startOffset: 10), p(90, 1, "explorer", window: true))
        XCTAssertNil(HostChain.resolve(startPid: 100, table: w, catalog: KnownHosts.windows))
    }

    func testMissingParentOrMissingStartStopsTheWalk() {
        XCTAssertNil(HostChain.resolve(startPid: 100, table: table(p(100, 999, "node")), catalog: KnownHosts.mac))
        XCTAssertNil(HostChain.resolve(startPid: 5, table: table(p(100, 1, "node")), catalog: KnownHosts.mac))
    }

    func testAParentYoungerThanItsChildIsAReusedPid() {
        let t = table(p(100, 90, "node"), p(90, 1, "com.apple.Terminal", window: true, startOffset: 60))
        XCTAssertNil(HostChain.resolve(startPid: 100, table: t, catalog: KnownHosts.mac))
    }

    func testAProcessThatIsItsOwnParentStopsTheWalk() {
        XCTAssertNil(HostChain.resolve(startPid: 100, table: table(p(100, 100, "node")), catalog: KnownHosts.mac))
    }

    func testWalkIsCappedAtTwelveProcesses() {
        func chain(_ nodesBeforeHost: Int) -> [Int: ProcessRecord] {
            var records: [ProcessRecord] = []
            for i in 0..<nodesBeforeHost {
                records.append(p(100 + i, 101 + i, "node", startOffset: TimeInterval(nodesBeforeHost - i)))
            }
            records.append(p(100 + nodesBeforeHost, 1, "com.apple.Terminal", window: true))
            return Dictionary(uniqueKeysWithValues: records.map { ($0.pid, $0) })
        }
        XCTAssertNotNil(HostChain.resolve(startPid: 100, table: chain(HostChain.maxDepth - 1), catalog: KnownHosts.mac))
        XCTAssertNil(HostChain.resolve(startPid: 100, table: chain(HostChain.maxDepth), catalog: KnownHosts.mac))
    }

    func testCatalogsMapNamesAndStopWords() {
        XCTAssertEqual(KnownHosts.mac.displayName("com.apple.Terminal"), "Terminal")
        XCTAssertEqual(KnownHosts.mac.displayName("com.microsoft.VSCode"), "VS Code")
        XCTAssertEqual(KnownHosts.mac.displayName("com.jetbrains.intellij"), "JetBrains")
        XCTAssertNil(KnownHosts.mac.displayName("com.example.other"))
        XCTAssertTrue(KnownHosts.mac.stopsWalk("com.apple.finder"))
        XCTAssertFalse(KnownHosts.mac.stopsWalk("com.apple.Terminal"))
        XCTAssertEqual(KnownHosts.windows.displayName("mintty"), "Git Bash")
        XCTAssertEqual(KnownHosts.windows.displayName("rider64"), "JetBrains")
        XCTAssertTrue(KnownHosts.windows.stopsWalk("explorer"))
        XCTAssertFalse(KnownHosts.windows.stopsWalk("code"))
    }
}
