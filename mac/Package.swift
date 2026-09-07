// swift-tools-version: 6.0
import PackageDescription

var targets: [Target] = [
    .target(
        name: "ClaudeToolbarCore",
        path: "Sources/ClaudeToolbarCore",
        swiftSettings: [.swiftLanguageMode(.v5)]
    ),
    .testTarget(
        name: "ClaudeToolbarCoreTests",
        dependencies: ["ClaudeToolbarCore"],
        path: "Tests/ClaudeToolbarCoreTests",
        swiftSettings: [.swiftLanguageMode(.v5)]
    ),
]

var products: [Product] = [
    .library(name: "ClaudeToolbarCore", targets: ["ClaudeToolbarCore"]),
]

#if os(macOS)
targets.append(
    .executableTarget(
        name: "ClaudeToolbar",
        dependencies: ["ClaudeToolbarCore"],
        path: "Sources/ClaudeToolbar",
        swiftSettings: [.swiftLanguageMode(.v5)],
        linkerSettings: [
            .linkedFramework("AppKit"),
            .linkedFramework("SwiftUI"),
            .linkedFramework("ServiceManagement"),
            .linkedFramework("Network"),
        ]
    )
)
products.append(.executable(name: "ClaudeToolbar", targets: ["ClaudeToolbar"]))
#endif

let package = Package(
    name: "ClaudeToolbar",
    platforms: [.macOS(.v14)],
    products: products,
    targets: targets
)
