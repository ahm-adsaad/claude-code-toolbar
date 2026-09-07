import XCTest
@testable import ClaudeToolbarCore

final class ChimeSynthTests: XCTestCase {
    private func int32(_ data: Data, _ offset: Int) -> Int32 { data.subdata(in: offset..<offset + 4).withUnsafeBytes { $0.loadUnaligned(as: Int32.self) } }
    private func int16(_ data: Data, _ offset: Int) -> Int16 { data.subdata(in: offset..<offset + 2).withUnsafeBytes { $0.loadUnaligned(as: Int16.self) } }
    private func ascii(_ data: Data, _ offset: Int) -> String { String(decoding: data.subdata(in: offset..<offset + 4), as: UTF8.self) }

    func testWavHeaderMatchesTheSamples() {
        for kind in [ChimeKind.attention, .finished, .failed] {
            let samples = ChimeSynth.samples(kind)
            let wav = ChimeSynth.wav(kind)
            XCTAssertEqual(wav.count, 44 + samples.count * 2, "\(kind)")
            XCTAssertEqual(ascii(wav, 0), "RIFF")
            XCTAssertEqual(ascii(wav, 8), "WAVE")
            XCTAssertEqual(ascii(wav, 12), "fmt ")
            XCTAssertEqual(int16(wav, 20), 1)
            XCTAssertEqual(int16(wav, 22), 1)
            XCTAssertEqual(int32(wav, 24), Int32(ChimeSynth.sampleRate))
            XCTAssertEqual(int16(wav, 34), 16)
            XCTAssertEqual(ascii(wav, 36), "data")
            XCTAssertEqual(int32(wav, 40), Int32(samples.count * 2))
            XCTAssertTrue(samples.allSatisfy { abs($0) <= ChimeSynth.amplitude + 1e-9 })
            XCTAssertTrue(samples.contains { abs($0) > ChimeSynth.amplitude * 0.9 })
        }
    }

    func testDurationsFollowTheNotes() {
        XCTAssertEqual(ChimeSynth.notes(.attention).count, 2)
        XCTAssertEqual(ChimeSynth.notes(.finished).count, 1)
        XCTAssertEqual(ChimeSynth.notes(.failed).count, 2)
        let finished = ChimeSynth.samples(.finished)
        XCTAssertEqual(finished.count, Int((0.25 + 0.02) * Double(ChimeSynth.sampleRate)))
        XCTAssertEqual(finished[0], 0, accuracy: 1e-6)
        XCTAssertEqual(finished[finished.count - 1], 0, accuracy: 1e-3)
    }
}
