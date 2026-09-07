import AppKit
import ClaudeToolbarCore

let arguments = Array(CommandLine.arguments.dropFirst())

if arguments.first == "--version" {
    print("ClaudeToolbar \(AppInfo.version)")
    exit(0)
}

MainActor.assumeIsolated {
    let application = NSApplication.shared
    let appDelegate = AppDelegate()
    application.delegate = appDelegate
    application.setActivationPolicy(.accessory)
    application.run()
}
