import SwiftUI
import ClaudeToolbarCore

struct BarColors: Equatable {
    let ok: Color
    let warn: Color
    let crit: Color

    init(settings: AppSettings) {
        ok = Color(nsColor: NSColor(argbHex: settings.appearance.barOk) ?? .systemGreen)
        warn = Color(nsColor: NSColor(argbHex: settings.appearance.barWarn) ?? .systemOrange)
        crit = Color(nsColor: NSColor(argbHex: settings.appearance.barCrit) ?? .systemRed)
    }

    func color(for level: BarLevel) -> Color {
        switch level {
        case .ok: return ok
        case .warn: return warn
        case .crit: return crit
        }
    }
}

struct UsageBar: View {
    let fraction: Double
    let color: Color

    var body: some View {
        GeometryReader { geometry in
            let clamped = min(max(fraction, 0), 1)
            ZStack(alignment: .leading) {
                Capsule().fill(Color.primary.opacity(0.15))
                if clamped > 0 {
                    Capsule().fill(color).frame(width: max(6, geometry.size.width * clamped))
                }
            }
        }
        .frame(height: 6)
    }
}

struct PopoverView: View {
    let model: PopoverModel
    let colors: BarColors
    var sessionSummary: String?
    @Binding var launchAtLogin: Bool
    let onRefresh: () -> Void
    let onSettings: () -> Void
    let onQuit: () -> Void

    private var showsSignInHint: Bool {
        model.rows.isEmpty && model.statusText.hasSuffix("run claude")
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            if model.rows.isEmpty {
                if let sessionSummary {
                    Text(sessionSummary).font(.system(size: 11, weight: .medium))
                }
                Text(model.statusText)
                    .font(.system(size: 13, weight: .semibold))
                if showsSignInHint {
                    Text("Run claude in a terminal, then click Refresh now.")
                        .font(.system(size: 11))
                        .foregroundStyle(.secondary)
                }
            } else {
                ForEach(model.rows, id: \.name) { row in
                    VStack(alignment: .leading, spacing: 5) {
                        HStack {
                            Text(row.name).font(.system(size: 13, weight: .semibold))
                            Spacer()
                            Text(row.percentText).font(.system(size: 13, weight: .medium).monospacedDigit())
                        }
                        UsageBar(fraction: row.utilization / 100, color: colors.color(for: row.level))
                        if let reset = row.resetText {
                            Text([reset, row.resetClockText].compactMap { $0 }.joined(separator: " · "))
                                .font(.system(size: 11))
                                .foregroundStyle(.secondary)
                        }
                    }
                }
                VStack(alignment: .leading, spacing: 2) {
                    if let sessionSummary {
                        Text(sessionSummary).font(.system(size: 11, weight: .medium))
                    }
                    if let updated = model.updatedText {
                        Text(updated).font(.system(size: 11)).foregroundStyle(.secondary)
                    }
                    Text(model.statusText).font(.system(size: 11)).foregroundStyle(.secondary)
                }
            }

            Divider()

            HStack {
                Button("Refresh now", action: onRefresh)
                Spacer()
                Button("Settings…", action: onSettings)
            }
            Toggle("Launch at login", isOn: $launchAtLogin)
                .toggleStyle(.switch)
                .controlSize(.small)
            Button("Quit Claude Toolbar", action: onQuit)
        }
        .padding(14)
        .frame(width: 300)
    }
}
