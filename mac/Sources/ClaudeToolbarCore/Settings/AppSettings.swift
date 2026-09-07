import Foundation

public struct AppearanceSettings: Codable, Equatable, Sendable {
    public var preset: String = Presets.defaultName
    public var barOk: String = "#FF3FB950"
    public var barWarn: String = "#FFD29922"
    public var barCrit: String = "#FFF85149"
    public var warnThreshold: Int = 70
    public var critThreshold: Int = 90

    public init() {}

    private enum CodingKeys: String, CodingKey {
        case preset, barOk, barWarn, barCrit, warnThreshold, critThreshold
    }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        preset = try c.decodeIfPresent(String.self, forKey: .preset) ?? preset
        barOk = try c.decodeIfPresent(String.self, forKey: .barOk) ?? barOk
        barWarn = try c.decodeIfPresent(String.self, forKey: .barWarn) ?? barWarn
        barCrit = try c.decodeIfPresent(String.self, forKey: .barCrit) ?? barCrit
        warnThreshold = try c.decodeIfPresent(Int.self, forKey: .warnThreshold) ?? warnThreshold
        critThreshold = try c.decodeIfPresent(Int.self, forKey: .critThreshold) ?? critThreshold
    }
}

public struct RowSettings: Codable, Equatable, Sendable {
    public var showFiveHour = true
    public var showSevenDay = true
    public var showSevenDayOpus = false
    public var showSevenDaySonnet = false
    public var showLabel = true
    public var showBar = true
    public var showPercent = true
    public var showTime = false
    public var barWidth: Double = 40

    public init() {}

    private enum CodingKeys: String, CodingKey {
        case showFiveHour, showSevenDay, showSevenDayOpus, showSevenDaySonnet
        case showLabel, showBar, showPercent, showTime, barWidth
    }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        showFiveHour = try c.decodeIfPresent(Bool.self, forKey: .showFiveHour) ?? showFiveHour
        showSevenDay = try c.decodeIfPresent(Bool.self, forKey: .showSevenDay) ?? showSevenDay
        showSevenDayOpus = try c.decodeIfPresent(Bool.self, forKey: .showSevenDayOpus) ?? showSevenDayOpus
        showSevenDaySonnet = try c.decodeIfPresent(Bool.self, forKey: .showSevenDaySonnet) ?? showSevenDaySonnet
        showLabel = try c.decodeIfPresent(Bool.self, forKey: .showLabel) ?? showLabel
        showBar = try c.decodeIfPresent(Bool.self, forKey: .showBar) ?? showBar
        showPercent = try c.decodeIfPresent(Bool.self, forKey: .showPercent) ?? showPercent
        showTime = try c.decodeIfPresent(Bool.self, forKey: .showTime) ?? showTime
        barWidth = try c.decodeIfPresent(Double.self, forKey: .barWidth) ?? barWidth
    }
}

public struct BehaviorSettings: Codable, Equatable, Sendable {
    public var refreshIntervalSeconds = 60
    public var launchAtLogin = true
    public var mascot = MascotMode.full

    public init() {}

    private enum CodingKeys: String, CodingKey {
        case refreshIntervalSeconds, launchAtLogin, mascot
    }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        refreshIntervalSeconds = try c.decodeIfPresent(Int.self, forKey: .refreshIntervalSeconds) ?? refreshIntervalSeconds
        launchAtLogin = try c.decodeIfPresent(Bool.self, forKey: .launchAtLogin) ?? launchAtLogin
        mascot = try c.decodeIfPresent(String.self, forKey: .mascot) ?? mascot
    }
}

public struct NotificationSettings: Codable, Equatable, Sendable {
    public var enabled = true
    public var port = 47831
    public var sound = true

    public init() {}

    private enum CodingKeys: String, CodingKey { case enabled, port, sound }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        enabled = try c.decodeIfPresent(Bool.self, forKey: .enabled) ?? enabled
        port = try c.decodeIfPresent(Int.self, forKey: .port) ?? port
        sound = try c.decodeIfPresent(Bool.self, forKey: .sound) ?? sound
    }
}

public struct AppSettings: Codable, Equatable, Sendable {
    public var version = 1
    public var appearance = AppearanceSettings()
    public var rows = RowSettings()
    public var behavior = BehaviorSettings()
    public var notifications = NotificationSettings()

    public init() {}

    public static func createDefault() -> AppSettings { AppSettings() }

    private enum CodingKeys: String, CodingKey {
        case version, appearance, rows, behavior, notifications
    }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        version = try c.decodeIfPresent(Int.self, forKey: .version) ?? version
        appearance = try c.decodeIfPresent(AppearanceSettings.self, forKey: .appearance) ?? appearance
        rows = try c.decodeIfPresent(RowSettings.self, forKey: .rows) ?? rows
        behavior = try c.decodeIfPresent(BehaviorSettings.self, forKey: .behavior) ?? behavior
        notifications = try c.decodeIfPresent(NotificationSettings.self, forKey: .notifications) ?? notifications
    }
}

public enum SettingsJSON {
    public static func encode(_ settings: AppSettings) throws -> String {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        return String(decoding: try encoder.encode(settings), as: UTF8.self)
    }

    public static func decode(_ json: String) throws -> AppSettings {
        try JSONDecoder().decode(AppSettings.self, from: Data(json.utf8))
    }
}
