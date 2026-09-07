import AppKit
import SwiftUI

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
            colorSpaceName: .calibratedRGB, bytesPerRow: 0, bitsPerPixel: 0) else {
            throw SnapshotError.bitmap
        }
        // Must be set before the context is created: the context's base CTM is
        // derived from the point-size-to-pixel-size ratio at construction time.
        rep.size = size
        guard let context = NSGraphicsContext(bitmapImageRep: rep) else {
            throw SnapshotError.bitmap
        }

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

    /// Hosts a SwiftUI view in an offscreen window, lets it lay out, and captures it as PNG (1x).
    @MainActor
    static func pngData<Content: View>(view: Content, appearance: NSAppearance.Name, size: NSSize? = nil) throws -> Data {
        let host = NSHostingView(rootView: view)
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 400, height: 400),
                              styleMask: [.borderless], backing: .buffered, defer: false)
        window.appearance = NSAppearance(named: appearance)
        window.isReleasedWhenClosed = false
        window.contentView = host
        let target = size ?? host.fittingSize
        window.setContentSize(target)
        host.frame = NSRect(origin: .zero, size: target)
        window.orderFrontRegardless()
        host.layoutSubtreeIfNeeded()
        RunLoop.main.run(until: Date().addingTimeInterval(0.3))

        guard let rep = host.bitmapImageRepForCachingDisplay(in: host.bounds) else { throw SnapshotError.bitmap }
        host.cacheDisplay(in: host.bounds, to: rep)
        window.orderOut(nil)
        guard let data = rep.representation(using: .png, properties: [:]) else { throw SnapshotError.png }
        return data
    }
}
