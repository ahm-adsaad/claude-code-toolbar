import AppKit

enum ViewSnapshot {
    enum SnapshotError: Error, CustomStringConvertible {
        case bitmap
        case png
        case write(String)

        var description: String {
            switch self {
            case .bitmap: return "could not create a bitmap context"
            case .png: return "could not encode PNG"
            case .write(let path): return "could not write \(path)"
            }
        }
    }

    static let lightBackdrop = NSColor(srgbRed: 0.93, green: 0.93, blue: 0.93, alpha: 1)
    static let darkBackdrop = NSColor(srgbRed: 0.12, green: 0.12, blue: 0.12, alpha: 1)

    /// Draws `image` on a padded backdrop at `scale`x and returns PNG bytes.
    static func pngData(image: NSImage, backdrop: NSColor, padding: CGFloat = 8, scale: CGFloat = 2) throws -> Data {
        let size = NSSize(width: image.size.width + padding * 2, height: image.size.height + padding * 2)
        guard let rep = NSBitmapImageRep(
            bitmapDataPlanes: nil,
            pixelsWide: Int(size.width * scale), pixelsHigh: Int(size.height * scale),
            bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
            colorSpaceName: .calibratedRGB, bytesPerRow: 0, bitsPerPixel: 0),
              let context = NSGraphicsContext(bitmapImageRep: rep) else {
            throw SnapshotError.bitmap
        }
        rep.size = size

        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = context
        backdrop.setFill()
        NSRect(origin: .zero, size: size).fill()
        image.draw(in: NSRect(x: padding, y: padding, width: image.size.width, height: image.size.height),
                   from: .zero, operation: .sourceOver, fraction: 1)
        context.flushGraphics()
        NSGraphicsContext.restoreGraphicsState()

        guard let data = rep.representation(using: .png, properties: [:]) else { throw SnapshotError.png }
        return data
    }

    static func write(_ data: Data, to directory: URL, name: String) throws {
        let url = directory.appendingPathComponent(name)
        do {
            try data.write(to: url)
        } catch {
            throw SnapshotError.write(url.path)
        }
    }
}
