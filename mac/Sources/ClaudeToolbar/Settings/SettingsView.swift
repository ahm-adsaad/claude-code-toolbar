import SwiftUI
import ClaudeToolbarCore

struct PreviewStrip: View {
    let image: NSImage
    let backdrop: Color

    var body: some View {
        HStack {
            Spacer()
            Image(nsImage: image)
            Spacer()
        }
        .padding(8)
        .background(backdrop)
        .clipShape(RoundedRectangle(cornerRadius: 6))
    }
}

struct SettingsView: View {
    @ObservedObject var model: SettingsModel
    var contentHeight: CGFloat = 620

    private var presetBinding: Binding<String> {
        Binding(get: { model.settings.appearance.preset }, set: { model.applyPreset($0) })
    }

    var body: some View {
        Form {
            Section("Preview") {
                PreviewStrip(image: model.previewImage(dark: true), backdrop: Color(nsColor: ViewSnapshot.darkBackdrop))
                PreviewStrip(image: model.previewImage(dark: false), backdrop: Color(nsColor: ViewSnapshot.lightBackdrop))
            }

            Section("Rows") {
                Toggle("5-hour session", isOn: $model.settings.rows.showFiveHour)
                Toggle("7-day weekly", isOn: $model.settings.rows.showSevenDay)
                Toggle("7-day Opus", isOn: $model.settings.rows.showSevenDayOpus)
                Toggle("7-day Sonnet", isOn: $model.settings.rows.showSevenDaySonnet)
                Toggle("Label", isOn: $model.settings.rows.showLabel)
                Toggle("Bar", isOn: $model.settings.rows.showBar)
                Toggle("Percent", isOn: $model.settings.rows.showPercent)
                Toggle("Time until reset", isOn: $model.settings.rows.showTime)
                HStack {
                    Text("Bar width")
                    Slider(value: $model.settings.rows.barWidth, in: 24...80, step: 2)
                    Text("\(Int(model.settings.rows.barWidth)) pt")
                        .monospacedDigit()
                        .frame(width: 44, alignment: .trailing)
                }
            }

            Section("Appearance") {
                Picker("Preset", selection: presetBinding) {
                    Text("Default").tag("default")
                    Text("Claude").tag("claude")
                    Text("Mono").tag("mono")
                    if model.settings.appearance.preset == Presets.custom {
                        Text("Custom").tag(Presets.custom)
                    }
                }
                ColorPicker("Bar OK", selection: model.colorBinding(\.barOk), supportsOpacity: false)
                ColorPicker("Bar warning", selection: model.colorBinding(\.barWarn), supportsOpacity: false)
                ColorPicker("Bar critical", selection: model.colorBinding(\.barCrit), supportsOpacity: false)
                Stepper("Warn at \(model.settings.appearance.warnThreshold)%", value: model.warnThreshold, in: 1...99)
                Stepper("Critical at \(model.settings.appearance.critThreshold)%", value: model.critThreshold, in: 2...100)
            }

            Section("Behaviour") {
                Stepper("Refresh every \(model.settings.behavior.refreshIntervalSeconds) s",
                        value: $model.settings.behavior.refreshIntervalSeconds, in: 30...300, step: 30)
                Toggle("Launch at login", isOn: $model.settings.behavior.launchAtLogin)
                if !model.launchAtLoginStatus.isEmpty {
                    Text(model.launchAtLoginStatus).font(.caption).foregroundStyle(.secondary)
                }
                Picker("Mascot", selection: $model.settings.behavior.mascot) {
                    Text("Waves on hover and at thresholds").tag(MascotMode.full)
                    Text("Waves on hover only").tag(MascotMode.hover)
                    Text("Off").tag(MascotMode.off)
                }
            }

            Section("Account") {
                LabeledContent("Source", value: model.account.credentials.source)
                LabeledContent("Login", value: model.loginText)
                if let plan = model.account.credentials.subscriptionType {
                    LabeledContent("Plan", value: plan)
                }
                LabeledContent("Last update", value: model.lastUpdateText)
                if model.needsClaude {
                    Text("Run claude in a terminal to refresh your login.").font(.caption).foregroundStyle(.secondary)
                }
                Button("Refresh now") { model.onRefresh() }
            }

            Section {
                Button("Reset to defaults") { model.resetToDefaults() }
            }
        }
        .formStyle(.grouped)
        .frame(width: 440, height: contentHeight)
    }
}
