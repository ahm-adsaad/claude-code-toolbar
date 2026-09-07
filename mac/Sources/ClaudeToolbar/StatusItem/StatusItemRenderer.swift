import AppKit
import ClaudeToolbarCore

struct StatusItemStyle {
    let barOk: NSColor
    let barWarn: NSColor
    let barCrit: NSColor
    let barWidth: CGFloat
    let showLabel: Bool
    let showBar: Bool
    let showPercent: Bool
    let showTime: Bool

    init(settings: AppSettings) {
        barOk = NSColor(argbHex: settings.appearance.barOk) ?? .systemGreen
        barWarn = NSColor(argbHex: settings.appearance.barWarn) ?? .systemOrange
        barCrit = NSColor(argbHex: settings.appearance.barCrit) ?? .systemRed
        barWidth = CGFloat(settings.rows.barWidth)
        showLabel = settings.rows.showLabel
        showBar = settings.rows.showBar
        showPercent = settings.rows.showPercent
        showTime = settings.rows.showTime
    }

    func color(for level: BarLevel) -> NSColor {
        switch level {
        case .ok: return barOk
        case .warn: return barWarn
        case .crit: return barCrit
        }
    }
}

/// Draws a `StatusItemModel` into a single-line image for the menu bar. Pure: same inputs, same image.
enum StatusItemRenderer {
    static let height: CGFloat = 18
    static let fontSize: CGFloat = 11
    static let barHeight: CGFloat = 6
    static let rowGap: CGFloat = 10
    static let partGap: CGFloat = 4
    static let dotDiameter: CGFloat = 4
    static let edgeInset: CGFloat = 2

    private enum Part {
        case gap(CGFloat)
        case text(NSAttributedString)
        case bar(width: CGFloat, fraction: CGFloat, fill: NSColor)
        case dot
        case mascot(MascotModel)

        var width: CGFloat {
            switch self {
            case .gap(let w): return w
            case .text(let s): return ceil(s.size().width)
            case .bar(let w, _, _): return w
            case .dot: return StatusItemRenderer.dotDiameter
            case .mascot: return MascotDrawing.width
            }
        }
    }

    static func render(model: StatusItemModel, mascot: MascotModel, settings: AppSettings, appearance: NSAppearance) -> NSImage {
        let textColor = resolvedLabelColor(in: appearance)
        let style = StatusItemStyle(settings: settings)
        let parts = layout(model: model, mascot: mascot, style: style, textColor: textColor)
        let width = max(8, parts.reduce(0) { $0 + $1.width })

        let image = NSImage(size: NSSize(width: width, height: height), flipped: false) { rect in
            draw(parts, in: rect, dimmed: model.dimmed, textColor: textColor)
            return true
        }
        image.isTemplate = false
        return image
    }

    private static func layout(model: StatusItemModel, mascot: MascotModel, style: StatusItemStyle, textColor: NSColor) -> [Part] {
        var parts: [Part] = [.gap(edgeInset)]

        if mascot.visible && model.notice == nil {
            parts.append(.mascot(mascot))
            parts.append(.gap(partGap + 2))
        }

        if let notice = model.notice {
            parts.append(.text(attributed(notice, color: textColor)))
        } else {
            for (index, row) in model.rows.enumerated() {
                if index > 0 { parts.append(.gap(rowGap)) }
                var rowParts: [Part] = []
                if style.showLabel { rowParts.append(.text(attributed(row.label, color: textColor))) }
                if style.showBar {
                    let fraction = row.hasData ? CGFloat(row.utilization / 100) : 0
                    rowParts.append(.bar(width: style.barWidth, fraction: fraction, fill: style.color(for: row.level)))
                }
                if style.showPercent { rowParts.append(.text(attributed(row.percentText, color: textColor))) }
                let hintHere = index == 0 && model.hint != nil
                if style.showTime && !row.timeText.isEmpty && !hintHere {
                    rowParts.append(.text(attributed(row.timeText, color: textColor)))
                }
                if hintHere, let hint = model.hint {
                    rowParts.append(.text(attributed(hint, color: textColor)))
                }
                if rowParts.isEmpty {
                    rowParts.append(.text(attributed(row.percentText, color: textColor)))
                }
                for (i, part) in rowParts.enumerated() {
                    if i > 0 { parts.append(.gap(partGap)) }
                    parts.append(part)
                }
            }
            if model.showStaleDot {
                parts.append(.gap(partGap + 2))
                parts.append(.dot)
            }
        }

        parts.append(.gap(edgeInset))
        return parts
    }

    private static func draw(_ parts: [Part], in rect: NSRect, dimmed: Bool, textColor: NSColor) {
        guard let context = NSGraphicsContext.current?.cgContext else { return }
        context.saveGState()
        if dimmed { context.setAlpha(0.5) }
        let trackColor = textColor.withAlphaComponent(0.25)

        var x: CGFloat = rect.minX
        for part in parts {
            switch part {
            case .gap(let w):
                x += w
            case .text(let string):
                let size = string.size()
                string.draw(at: NSPoint(x: x, y: rect.minY + (rect.height - size.height) / 2))
                x += ceil(size.width)
            case .bar(let width, let fraction, let fill):
                let barRect = NSRect(x: x, y: rect.minY + (rect.height - barHeight) / 2, width: width, height: barHeight)
                let track = NSBezierPath(roundedRect: barRect, xRadius: barHeight / 2, yRadius: barHeight / 2)
                trackColor.setFill()
                track.fill()
                if fraction > 0 {
                    NSGraphicsContext.saveGraphicsState()
                    track.addClip()
                    fill.setFill()
                    let fillWidth = max(barHeight, width * min(fraction, 1))
                    NSRect(x: barRect.minX, y: barRect.minY, width: fillWidth, height: barHeight).fill()
                    NSGraphicsContext.restoreGraphicsState()
                }
                x += width
            case .dot:
                let dotRect = NSRect(x: x, y: rect.minY + (rect.height - dotDiameter) / 2, width: dotDiameter, height: dotDiameter)
                textColor.withAlphaComponent(0.7).setFill()
                NSBezierPath(ovalIn: dotRect).fill()
                x += dotDiameter
            case .mascot(let mascot):
                MascotDrawing.draw(mascot, at: NSPoint(x: x, y: rect.minY + (rect.height - MascotDrawing.height) / 2))
                x += MascotDrawing.width
            }
        }
        context.restoreGState()
    }

    private static func attributed(_ text: String, color: NSColor) -> NSAttributedString {
        NSAttributedString(string: text, attributes: [
            .font: NSFont.monospacedDigitSystemFont(ofSize: fontSize, weight: .medium),
            .foregroundColor: color,
        ])
    }

    /// `labelColor` resolved for a specific appearance so the image matches the menu bar it is drawn in.
    static func resolvedLabelColor(in appearance: NSAppearance) -> NSColor {
        var resolved = NSColor.labelColor
        appearance.performAsCurrentDrawingAppearance {
            resolved = NSColor.labelColor.usingColorSpace(.sRGB) ?? NSColor.labelColor
        }
        return resolved
    }
}
