// 2026-11-08 content-defined chunking for small patches (shared with AppUpdate1022.Cdc in C#; keep both identical).
// Gear rolling hash: h = (h << 1) &+ gear[byte]; cut when chunk >= 256 KiB and (h & mask) == 0, or at 4 MiB.
// gear[] comes from splitmix64 seeded with 0x5EED1107. Usage: cdc <file>  -> "offset size sha256" per line.
import Foundation
import CryptoKit

var state: UInt64 = 0x5EED1107
func splitmix() -> UInt64 {
    state &+= 0x9E3779B97F4A7C15
    var z = state
    z = (z ^ (z >> 30)) &* 0xBF58476D1CE4E5B9
    z = (z ^ (z >> 27)) &* 0x94D049BB133111EB
    return z ^ (z >> 31)
}
var gear = [UInt64](repeating: 0, count: 256)
for i in 0..<256 { gear[i] = splitmix() }
let minSize = 256 * 1024, maxSize = 4 * 1024 * 1024
let mask: UInt64 = (1 << 20) - 1   // ~1 MiB average

let data = try! Data(contentsOf: URL(fileURLWithPath: CommandLine.arguments[1]), options: .alwaysMapped)
var out = ""
data.withUnsafeBytes { (raw: UnsafeRawBufferPointer) in
    let p = raw.bindMemory(to: UInt8.self)
    let n = p.count
    var start = 0
    while start < n {
        var h: UInt64 = 0
        var i = start
        let limit = min(n, start + maxSize)
        var cut = limit
        while i < limit {
            h = (h << 1) &+ gear[Int(p[i])]
            i += 1
            if i - start >= minSize && (h & mask) == 0 { cut = i; break }
        }
        let digest = SHA256.hash(data: UnsafeRawBufferPointer(rebasing: raw[start..<cut]))
        out += "\(start) \(cut - start) \(digest.map { String(format: "%02x", $0) }.joined())\n"
        start = cut
    }
}
FileHandle.standardOutput.write(out.data(using: .utf8)!)
