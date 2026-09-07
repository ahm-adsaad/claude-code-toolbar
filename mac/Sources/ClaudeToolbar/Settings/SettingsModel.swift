import AppKit
import SwiftUI
import ClaudeToolbarCore

/// Source of truth for the settings window. Applies changes live at once and saves them 300 ms after the last change.
@MainActor
final class SettingsModel: ObservableObject {
    @Published var settings: AppSettings {
        didSet {
            guard settings != oldValue else { return }
            onApply(settings)
            scheduleSave()
        }
    }
    @Published var account: MonitorState
    @Published var launchAtLoginStatus: String

    let onApply: (AppSettings) -> Void
    let onSave: (AppSettings) -> Void
    let onRefresh: () -> Void
    private var pendingSave: DispatchWorkItem?

    private static let clockFormatter: DateFormatter = {
        let f = DateFormatter()
        f.dateStyle = .medium
        f.timeStyle = .short
        return f
    }()

    init(settings: AppSettings, account: MonitorState, launchAtLoginStatus: String,
         onApply: @escaping (AppSettings) -> Void, onSave: @escaping (AppSettings) -> Void, onRefresh: @escaping () -> Void) {
        self.settings = settings
        self.account = account
        self.launchAtLoginStatus = launchAtLoginStatus
        self.onApply = onApply
        self.onSave = onSave
        self.onRefresh = onRefresh
    }

    private func scheduleSave() {
        pendingSave?.cancel()
        let item = DispatchWorkItem { [weak self] in
            guard let self else { return }
            self.onSave(self.settings)
        }
        pendingSave = item
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3, execute: item)
    }

    func flushPendingSave() {
        if let item = pendingSave, !item.isCancelled {
            item.cancel()
            onSave(settings)
        }
        pendingSave = nil
    }

    func applyPreset(_ name: String) {
        var appearance = settings.appearance
        if Presets.apply(name, to: &appearance) {
            settings.appearance = appearance
        }
    }

    func resetToDefaults() {
        settings = SettingsValidator.normalize(AppSettings.createDefault())
    }

    func colorBinding(_ keyPath: WritableKeyPath<AppearanceSettings, String>) -> Binding<Color> {
        Binding(
            get: { Color(nsColor: NSColor(argbHex: self.settings.appearance[keyPath: keyPath]) ?? .systemGray) },
            set: { newColor in
                var appearance = self.settings.appearance
                appearance[keyPath: keyPath] = NSColor(newColor).argbHex
                appearance.preset = Presets.matching(appearance)
                self.settings.appearance = appearance
            })
    }

    var warnThreshold: Binding<Int> {
        Binding(
            get: { self.settings.appearance.warnThreshold },
            set: { value in
                var a = self.settings.appearance
                a.warnThreshold = min(max(value, 1), 99)
                if a.critThreshold <= a.warnThreshold { a.critThreshold = min(a.warnThreshold + 1, 100) }
                self.settings.appearance = a
            })
    }

    var critThreshold: Binding<Int> {
        Binding(
            get: { self.settings.appearance.critThreshold },
            set: { value in
                var a = self.settings.appearance
                a.critThreshold = min(max(value, 2), 100)
                if a.warnThreshold >= a.critThreshold { a.warnThreshold = max(a.critThreshold - 1, 1) }
                self.settings.appearance = a
            })
    }

    func previewImage(dark: Bool) -> NSImage {
        let model = StatusItemModelBuilder.build(state: SampleData.state(.ok, snapshot: SampleData.fullSnapshot), settings: settings, now: SampleData.now)
        let appearance = NSAppearance(named: dark ? .darkAqua : .aqua) ?? NSAppearance.currentDrawing()
        return StatusItemRenderer.render(model: model, settings: settings, appearance: appearance)
    }

    var loginText: String {
        switch account.credentials {
        case .valid(_, _, let expiresAt, _): return "Valid until \(Self.clockFormatter.string(from: expiresAt))"
        case .expired(_, let expiresAt, _): return "Expired at \(Self.clockFormatter.string(from: expiresAt))"
        case .missing: return "Not signed in"
        case .invalid(_, let reason): return "Invalid: \(reason)"
        }
    }

    var lastUpdateText: String {
        guard let last = account.lastSuccess else { return "never" }
        return "\(AgoFormatter.format(ago: Date().timeIntervalSince(last))) ago"
    }

    var needsClaude: Bool {
        account.status == .expired || account.status == .noCredentials
    }
}
