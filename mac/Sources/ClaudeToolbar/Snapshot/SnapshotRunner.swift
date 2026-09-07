import AppKit
import SwiftUI
import ClaudeToolbarCore

/// Renders every view with sample data into PNG files so the UI can be reviewed without a Mac.
/// Main-actor because later tasks add SwiftUI hosting views here.
@MainActor
enum SnapshotRunner {
    static func run(outputDirectory: String) throws {
        let directory = URL(fileURLWithPath: outputDirectory, isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try writeStatusItems(to: directory)
        try writePopovers(to: directory)
        try writeSettings(to: directory)
        print("snapshots written to \(directory.path)")
    }

    static func writeStatusItems(to directory: URL) throws {
        guard let light = NSAppearance(named: .aqua), let dark = NSAppearance(named: .darkAqua) else {
            throw ViewSnapshot.SnapshotError.bitmap
        }
        let defaults = SettingsValidator.normalize(AppSettings.createDefault())

        func model(_ status: UsageStatus, settings: AppSettings = defaults, snapshot: UsageSnapshot? = SampleData.snapshot) -> StatusItemModel {
            StatusItemModelBuilder.build(state: SampleData.state(status, snapshot: snapshot), settings: settings, now: SampleData.now)
        }

        func write(_ name: String, _ model: StatusItemModel, settings: AppSettings, dark isDark: Bool) throws {
            let image = StatusItemRenderer.render(model: model, settings: settings, appearance: isDark ? dark : light)
            let data = try ViewSnapshot.pngData(image: image, backdrop: isDark ? ViewSnapshot.darkBackdrop : ViewSnapshot.lightBackdrop)
            try ViewSnapshot.write(data, to: directory, name: name)
        }

        try write("statusitem-light.png", model(.ok), settings: defaults, dark: false)
        try write("statusitem-dark.png", model(.ok), settings: defaults, dark: true)
        try write("statusitem-stale-dark.png", model(.stale), settings: defaults, dark: true)
        try write("statusitem-expired-dark.png", model(.expired), settings: defaults, dark: true)
        try write("statusitem-nocreds-dark.png", model(.noCredentials, snapshot: nil), settings: defaults, dark: true)
        try write("statusitem-loading-dark.png", model(.loading, snapshot: nil), settings: defaults, dark: true)

        var allToggles = defaults
        allToggles.rows.showSevenDayOpus = true
        allToggles.rows.showSevenDaySonnet = true
        allToggles.rows.showTime = true
        try write("statusitem-alltoggles-dark.png", model(.ok, settings: allToggles, snapshot: SampleData.fullSnapshot), settings: allToggles, dark: true)

        var claude = defaults
        Presets.apply("claude", to: &claude.appearance)
        try write("statusitem-claude-light.png", model(.ok, settings: claude), settings: claude, dark: false)

        var textOnly = defaults
        textOnly.rows.showBar = false
        textOnly.rows.showTime = true
        try write("statusitem-textonly-dark.png", model(.ok, settings: textOnly), settings: textOnly, dark: true)
    }

    static func writePopovers(to directory: URL) throws {
        let defaults = SettingsValidator.normalize(AppSettings.createDefault())
        let clock: (Date) -> String = { date in
            let f = DateFormatter()
            f.locale = Locale(identifier: "en_US")
            f.timeZone = TimeZone(secondsFromGMT: 0)
            f.setLocalizedDateFormatFromTemplate("EEE j:mm")
            return f.string(from: date)
        }

        func view(_ status: UsageStatus, snapshot: UsageSnapshot? = SampleData.snapshot) -> some View {
            let model = PopoverModelBuilder.build(state: SampleData.state(status, snapshot: snapshot), settings: defaults, now: SampleData.now, formatClock: clock)
            return PopoverView(model: model, colors: BarColors(settings: defaults), launchAtLogin: .constant(true),
                               onRefresh: {}, onSettings: {}, onQuit: {})
                .background(Color(nsColor: .windowBackgroundColor))
        }

        try ViewSnapshot.write(try ViewSnapshot.pngData(view: view(.ok), appearance: .aqua), to: directory, name: "popover-light.png")
        try ViewSnapshot.write(try ViewSnapshot.pngData(view: view(.ok), appearance: .darkAqua), to: directory, name: "popover-dark.png")
        try ViewSnapshot.write(try ViewSnapshot.pngData(view: view(.ok, snapshot: SampleData.fullSnapshot), appearance: .darkAqua), to: directory, name: "popover-all-dark.png")
        try ViewSnapshot.write(try ViewSnapshot.pngData(view: view(.stale), appearance: .darkAqua), to: directory, name: "popover-stale-dark.png")
        try ViewSnapshot.write(try ViewSnapshot.pngData(view: view(.expired), appearance: .darkAqua), to: directory, name: "popover-expired-dark.png")
        try ViewSnapshot.write(try ViewSnapshot.pngData(view: view(.noCredentials, snapshot: nil), appearance: .darkAqua), to: directory, name: "popover-nocreds-dark.png")
    }

    static func writeSettings(to directory: URL) throws {
        let defaults = SettingsValidator.normalize(AppSettings.createDefault())
        let size = NSSize(width: 440, height: 1500)
        for (name, appearance, status) in [
            ("settings-light.png", NSAppearance.Name.aqua, UsageStatus.ok),
            ("settings-dark.png", NSAppearance.Name.darkAqua, UsageStatus.ok),
            ("settings-expired-dark.png", NSAppearance.Name.darkAqua, UsageStatus.expired),
        ] {
            let model = SettingsModel(settings: defaults, account: SampleData.state(status), launchAtLoginStatus: "enabled",
                                      onApply: { _ in }, onSave: { _ in }, onRefresh: {})
            let data = try ViewSnapshot.pngData(view: SettingsView(model: model), appearance: appearance, size: size)
            try ViewSnapshot.write(data, to: directory, name: name)
        }
    }
}
