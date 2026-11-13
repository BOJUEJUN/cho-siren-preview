// 2026-11-12: cut a member portrait off its white backdrop (Vision foreground core + white key, like matte.swift).
//   swiftc -O cutout_still.swift -o cutout && ./cutout in.png out.png
import AppKit
import CoreImage
import Vision

let args = CommandLine.arguments
guard args.count == 3, let src = NSImage(contentsOfFile: args[1]),
      let cg = src.cgImage(forProposedRect: nil, context: nil, hints: nil) else { print("usage: cutout in.png out.png"); exit(1) }
let w = cg.width, h = cg.height
var px = [UInt8](repeating: 0, count: w * h * 4)
let cs = CGColorSpace(name: CGColorSpace.sRGB)!
let ctx = CGContext(data: &px, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: cs,
                    bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
ctx.draw(cg, in: CGRect(x: 0, y: 0, width: w, height: h))
// backdrop: median-ish of the four corners
var bg = (0.0, 0.0, 0.0)
for (x, y) in [(2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3)] {
    let i = (y * w + x) * 4; bg.0 += Double(px[i]) / 4; bg.1 += Double(px[i + 1]) / 4; bg.2 += Double(px[i + 2]) / 4
}
let req = VNGenerateForegroundInstanceMaskRequest()
let handler = VNImageRequestHandler(cgImage: cg)
try handler.perform([req])
guard let obs = req.results?.first else { print("no foreground"); exit(2) }
let maskBuf = try obs.generateScaledMaskForImage(forInstances: obs.allInstances, from: handler)
CVPixelBufferLockBaseAddress(maskBuf, .readOnly)
let mw = CVPixelBufferGetWidth(maskBuf), mh = CVPixelBufferGetHeight(maskBuf), mstride = CVPixelBufferGetBytesPerRow(maskBuf)
let mbase = CVPixelBufferGetBaseAddress(maskBuf)!.assumingMemoryBound(to: Float32.self)
var out = [UInt8](repeating: 0, count: w * h * 4)
for y in 0..<h {
    for x in 0..<w {
        let i = (y * w + x) * 4
        let r = Double(px[i]), g = Double(px[i + 1]), b = Double(px[i + 2])
        let mx = min(mw - 1, x * mw / w), my = min(mh - 1, y * mh / h)
        let core = Double(mbase[my * mstride / 4 + mx])
        let dist = max(abs(bg.0 - r), max(abs(bg.1 - g), abs(bg.2 - b))) / 255
        let key = max(0, min(1, (dist - 0.04) / 0.12))
        // Inside the figure trust Vision (white clothes stay solid); at the edge the white key decides.
        let a = core > 0.98 ? 1.0 : min(core * 1.2, 1) * key
        var fr = r, fg = g, fb = b
        if a > 0.004 && a < 0.999 {
            fr = max(0, min(255, (r - (1 - a) * bg.0) / a)); fg = max(0, min(255, (g - (1 - a) * bg.1) / a)); fb = max(0, min(255, (b - (1 - a) * bg.2) / a))
        }
        out[i] = UInt8(fr * a); out[i + 1] = UInt8(fg * a); out[i + 2] = UInt8(fb * a); out[i + 3] = UInt8((a * 255).rounded())
    }
}
let octx = CGContext(data: &out, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: cs,
                     bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
let rep = NSBitmapImageRep(cgImage: octx.makeImage()!)
try rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: args[2]))
print("cut", w, h, "backdrop", Int(bg.0), Int(bg.1), Int(bg.2))
