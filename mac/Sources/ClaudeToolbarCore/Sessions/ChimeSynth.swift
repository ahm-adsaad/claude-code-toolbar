import Foundation

public enum ChimeKind: Sendable { case attention, finished, failed }

/// Generates the notification chimes as 16-bit mono WAV data so no audio assets ship with the app.
public enum ChimeSynth {
    public static let sampleRate = 44_100
    public static let amplitude = 0.3
    private static let attackSeconds = 0.008
    private static let gapSeconds = 0.02

    public static func notes(_ kind: ChimeKind) -> [(hz: Double, seconds: Double)] {
        switch kind {
        case .attention: return [(659.25, 0.12), (880.0, 0.20)]
        case .finished: return [(523.25, 0.25)]
        case .failed: return [(392.0, 0.14), (329.63, 0.22)]
        }
    }

    public static func samples(_ kind: ChimeKind) -> [Double] {
        let notes = notes(kind)
        let total = Int(notes.reduce(0) { $0 + $1.seconds + gapSeconds } * Double(sampleRate))
        var buffer = [Double](repeating: 0, count: total)
        var offset = 0
        for note in notes {
            let count = Int(note.seconds * Double(sampleRate))
            let attack = Int(attackSeconds * Double(sampleRate))
            let fadeStart = Int(Double(count) * 0.4)
            var i = 0
            while i < count && offset + i < buffer.count {
                let envelope: Double
                if i < attack {
                    envelope = Double(i) / Double(attack)
                } else if i > fadeStart {
                    envelope = 1 - Double(i - fadeStart) / Double(count - fadeStart)
                } else {
                    envelope = 1
                }
                buffer[offset + i] = amplitude * envelope * sin(2 * .pi * note.hz * Double(i) / Double(sampleRate))
                i += 1
            }
            offset += count + Int(gapSeconds * Double(sampleRate))
        }
        return buffer
    }

    public static func wav(_ kind: ChimeKind) -> Data {
        let samples = samples(kind)
        let dataBytes = samples.count * 2
        var data = Data(capacity: 44 + dataBytes)
        func put(_ value: Int32) { withUnsafeBytes(of: value.littleEndian) { data.append(contentsOf: $0) } }
        func put(_ value: Int16) { withUnsafeBytes(of: value.littleEndian) { data.append(contentsOf: $0) } }
        data.append(contentsOf: Array("RIFF".utf8)); put(Int32(36 + dataBytes)); data.append(contentsOf: Array("WAVE".utf8))
        data.append(contentsOf: Array("fmt ".utf8)); put(Int32(16)); put(Int16(1)); put(Int16(1))
        put(Int32(sampleRate)); put(Int32(sampleRate * 2)); put(Int16(2)); put(Int16(16))
        data.append(contentsOf: Array("data".utf8)); put(Int32(dataBytes))
        for sample in samples { put(Int16((sample * Double(Int16.max)).rounded())) }
        return data
    }
}
