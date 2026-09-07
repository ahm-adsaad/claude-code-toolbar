import AppKit
import ClaudeToolbarCore

let arguments = Array(CommandLine.arguments.dropFirst())

if arguments.first == "--version" {
    print("ClaudeToolbar \(AppInfo.version)")
    exit(0)
}

if arguments.first == "--snapshot" {
    guard arguments.count >= 2 else {
        FileHandle.standardError.write(Data("usage: ClaudeToolbar --snapshot <directory>\n".utf8))
        exit(2)
    }
    MainActor.assumeIsolated {
        let snapshotApp = NSApplication.shared
        snapshotApp.setActivationPolicy(.prohibited)
        do {
            try SnapshotRunner.run(outputDirectory: arguments[1])
            exit(0)
        } catch {
            FileHandle.standardError.write(Data("snapshot failed: \(error)\n".utf8))
            exit(1)
        }
    }
}

// Hoisted to a top-level binding: NSApplication.delegate is a weak property,
// so a delegate created only inside the closure below would have no strong
// owner left after being assigned to it and could deallocate before it runs.
let appDelegate = MainActor.assumeIsolated { AppDelegate() }

MainActor.assumeIsolated {
    let application = NSApplication.shared
    application.delegate = appDelegate
    application.setActivationPolicy(.accessory)
    withExtendedLifetime(appDelegate) {
        application.run()
    }
}
