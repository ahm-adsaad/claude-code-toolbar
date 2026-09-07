import AppKit
import ClaudeToolbarCore

/// Writes the generated chimes to ~/Library/Application Support/ClaudeToolbar/sounds and plays them.
@MainActor
final class ChimePlayer {
    private var sounds: [ChimeKind: NSSound] = [:]

    init() {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? FileManager.default.homeDirectoryForCurrentUser
        let directory = base.appendingPathComponent("ClaudeToolbar/sounds", isDirectory: true)
        do {
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        } catch {
            Log.error("Could not create \(directory.path): \(error.localizedDescription)")
            return
        }
        for kind in [ChimeKind.attention, .finished, .failed] {
            let url = directory.appendingPathComponent("\(kind).wav")
            do {
                try ChimeSynth.wav(kind).write(to: url, options: .atomic)
                if let sound = NSSound(contentsOf: url, byReference: false) {
                    sounds[kind] = sound
                } else {
                    Log.error("Could not load chime \(kind) from \(url.path)")
                }
            } catch {
                Log.error("Could not write chime \(kind): \(error.localizedDescription)")
            }
        }
    }

    func play(_ kind: ChimeKind) {
        guard let sound = sounds[kind] else { return }
        sound.stop()
        sound.play()
    }
}
