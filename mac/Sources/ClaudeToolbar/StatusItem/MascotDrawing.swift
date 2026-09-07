import AppKit
import ClaudeToolbarCore

/// Clawd, drawn cell by cell from `ClawdSprite` into the current (y-up) graphics context.
enum MascotDrawing {
    static let cell: CGFloat = 1.5
    static let width = CGFloat(ClawdSprite.columns) * cell
    static let height = CGFloat(ClawdSprite.rows) * cell * 2

    private static let body = NSColor(argbHex: ClawdSprite.bodyColor) ?? .systemOrange
    private static let eye = NSColor(argbHex: ClawdSprite.eyeColor) ?? .black

    static func draw(_ model: MascotModel, at origin: NSPoint) {
        guard model.visible else { return }
        let rowHeight = cell * 2
        // The sprite is a grid of hard-edged rectangles: antialiasing them leaves pale seams
        // between neighbouring cells on a 1x display. The badge below wants it back on.
        let context = NSGraphicsContext.current
        let wasAntialiasing = context?.shouldAntialias ?? true
        context?.shouldAntialias = false
        for spriteCell in ClawdSprite.cells(for: ClawdSprite.poseFor(armAngle: model.armAngle)) {
            let rect = NSRect(
                x: origin.x + CGFloat(spriteCell.col) * cell,
                y: origin.y + CGFloat(ClawdSprite.rows - 1 - spriteCell.row) * rowHeight,
                width: cell, height: rowHeight)
            (spriteCell.kind == .eye ? eye : body).setFill()
            rect.fill()
        }
        context?.shouldAntialias = wasAntialiasing

        if model.badgeLit, let hex = model.badge.hex, let color = NSColor(argbHex: hex) {
            let radius = CGFloat(ClawdSprite.badgeRadiusCells) * cell
            // The badge sits half a row above the sprite; the menu bar image is exactly the
            // sprite's height, so keep the circle inside it instead of slicing off its top.
            let center = NSPoint(
                x: origin.x + CGFloat(ClawdSprite.badgeCenterCol) * cell,
                y: min(origin.y + (CGFloat(ClawdSprite.rows) - CGFloat(ClawdSprite.badgeCenterRow)) * rowHeight,
                       origin.y + height - radius))
            color.setFill()
            NSBezierPath(ovalIn: NSRect(x: center.x - radius, y: center.y - radius, width: radius * 2, height: radius * 2)).fill()
        }
    }
}
