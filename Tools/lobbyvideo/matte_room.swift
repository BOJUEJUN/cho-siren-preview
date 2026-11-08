// 2026-11-07 matte_room: variant for the practice-room clips (four chibis in a painted room, not on white).
// alpha = Vision's soft foreground mask averaged over +-2 frames, then tightened; no colour key and no
// un-premultiply (the clip is composited over the same room art, so a faint room-coloured edge is invisible).
// Usage as matte.swift. Original header follows.
// Lobby idol clips (2026-10-24): white-background 1080x1920 clips → a looping "stacked alpha" MP4 that every
// platform's VideoPlayer can play: left half = colour (white fringe removed), right half = alpha as grey.
// The matte is Apple Vision's foreground mask, tightened by a white key on the background side. The clip is
// written forward then backward (ping-pong) so the loop is seamless and every loop passes the shared first frame.
//   swiftc -O matte.swift -o matte && ./matte in.mov out.mp4 [colourWidth=native] [preview.png|-] [averageMbps]
// 2026-11-01: optional averageMbps overrides the resolution-scaled default. WebGL streams these clips, so the
// elf/mermaid loops use 12 (face PSNR within ~0.5-0.9 dB of 17.5 Mbps at ~2/3 the size; measurements in
// Builds/qa-logs/zj-1101/lobby-fix/video-tradeoff.txt).
// 2026-11-01 (video-hq, "最高清晰度、不要压缩画质"): out.mp4 = "-" writes the stacked frames as raw BGRA to
// stdout (ping-pong order) so export_hq.sh can encode them with x264 near-losslessly; "--probe" prints
// "outW outH fps" and exits. Logs go to stderr.
// 2026-11-01 (video-hq, "边缘不能闪烁"): Vision's "this is the figure" core is voted over +-coreVoteRadius frames.
// It alone decides whether a backdrop-coloured pixel stays solid, and it flickers for 1-7 frames at a time, which
// popped solid white patches in and out (lilith-a frame 34: 14k px; Builds/qa-logs/zj-1101b/video-hq/matte-white-pops.txt).
// A pixel stays solid only if this frame's core and the majority of the window agree, so it cannot touch pixels that
// are not backdrop-coloured and can only clear backdrop-coloured ones, never add any.
import AVFoundation
import CoreImage
import Foundation
import Vision

func print(_ items: Any...) {
    FileHandle.standardError.write((items.map { "\($0)" }.joined(separator: " ") + "\n").data(using: .utf8)!)
}

let args = CommandLine.arguments
guard args.count >= 3 else { print("usage: matte in.mov out.mp4|-|--probe [halfWidth] [preview.png|-] [averageMbps]"); exit(1) }
let rawOut = args[2] == "-", probeOnly = args[2] == "--probe"
let input = URL(fileURLWithPath: args[1]), output = URL(fileURLWithPath: args[2])
let previewPath = args.count > 4 && args[4] != "-" ? args[4] : nil
let requestedMbps = args.count > 5 ? Double(args[5]) : nil

let asset = AVURLAsset(url: input)
let track = asset.tracks(withMediaType: .video)[0]
let fps = track.nominalFrameRate > 0 ? track.nominalFrameRate : 30
let natural = track.naturalSize.applying(track.preferredTransform)
let srcW = Int(abs(natural.width)), srcH = Int(abs(natural.height))
// Keep source detail by default; an explicit width may downscale, never upscale.
let requestedWidth = args.count > 3 ? Int(args[3])! : srcW
let halfW = max(2, min(srcW, requestedWidth) / 2 * 2)
let halfH = Int((Double(halfW) * Double(srcH) / Double(srcW)).rounded() / 2) * 2
let outW = halfW * 2, outH = halfH
if probeOnly { Swift.print(outW, outH, fps); exit(0) }

let reader = try AVAssetReader(asset: asset)
let readOut = AVAssetReaderTrackOutput(track: track, outputSettings: [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA])
reader.add(readOut)
reader.startReading()

let ctx = CIContext(options: [.workingColorSpace: NSNull()])
var colours: [Data] = []  // BGRA, halfW x halfH, tightly packed
var cores: [Data] = []    // Vision soft mask 0..255, halfW x halfH
var backdrops: [(Double, Double, Double)] = []
let coreVoteRadius = 5

func bytes(_ buffer: CVPixelBuffer) -> (UnsafeMutablePointer<UInt8>, Int) {
    CVPixelBufferLockBaseAddress(buffer, [])
    return (CVPixelBufferGetBaseAddress(buffer)!.assumingMemoryBound(to: UInt8.self), CVPixelBufferGetBytesPerRow(buffer))
}

func scaled(_ image: CIImage, _ w: Int, _ h: Int) -> CVPixelBuffer {
    var pb: CVPixelBuffer?
    CVPixelBufferCreate(nil, w, h, kCVPixelFormatType_32BGRA, [kCVPixelBufferIOSurfacePropertiesKey as String: [:]] as CFDictionary, &pb)
    let sx = CGFloat(w) / image.extent.width, sy = CGFloat(h) / image.extent.height
    ctx.render(image.transformed(by: CGAffineTransform(scaleX: sx, y: sy)), to: pb!)
    return pb!
}

// Backdrop colour, re-sampled from every frame's corners: some generated clips open on a grey-green
// frame and then cut to white (2026-10-29 mermaid), so a first-frame sample leaves the white backdrop in.
var bgR = 255.0, bgG = 255.0, bgB = 255.0, bgSampled = false, lastBg = ""

var index = 0
while let sample = readOut.copyNextSampleBuffer(), let src = CMSampleBufferGetImageBuffer(sample) {
    autoreleasepool {
        let request = VNGenerateForegroundInstanceMaskRequest()
        let handler = VNImageRequestHandler(cvPixelBuffer: src, options: [:])
        try? handler.perform([request])
        var maskImage: CIImage? = nil
        if let result = request.results?.first,
           let mask = try? result.generateScaledMaskForImage(forInstances: result.allInstances, from: handler) {
            maskImage = CIImage(cvPixelBuffer: mask)
        }
        let color = scaled(CIImage(cvPixelBuffer: src), halfW, halfH)
        let maskPB = maskImage.map { scaled($0, halfW, halfH) }
        let (cp, crow) = bytes(color)
        var mp: UnsafeMutablePointer<UInt8>? = nil, mrow = 0
        if let m = maskPB { let r = bytes(m); mp = r.0; mrow = r.1 }
        do {
            bgSampled = true
            var sr = 0.0, sg = 0.0, sb = 0.0, n = 0.0
            var corners: [(Double, Double, Double)] = []
            for (cx, cy) in [(2, 2), (halfW - 10, 2), (2, halfH - 10), (halfW - 10, halfH - 10)] {
                var cr = 0.0, cg = 0.0, cb = 0.0
                for yy in cy..<(cy + 8) { for xx in cx..<(cx + 8) {
                    let c = cp + yy * crow + xx * 4; cb += Double(c[0]); cg += Double(c[1]); cr += Double(c[2])
                } }
                corners.append((cr / 64, cg / 64, cb / 64)); sr += cr; sg += cg; sb += cb; n += 64
            }
            // Only trust the sample when all four corners agree (a hair or tail in a corner would skew it).
            let spread = corners.map { c in corners.map { d in max(abs(c.0 - d.0), max(abs(c.1 - d.1), abs(c.2 - d.2))) }.max()! }.max()!
            if spread < 14 || index == 0 {
                bgR = sr / n; bgG = sg / n; bgB = sb / n
                if min(bgR, min(bgG, bgB)) > 235 { bgR = 255; bgG = 255; bgB = 255 }
            }
            let tag = "\(Int(bgR)) \(Int(bgG)) \(Int(bgB))"
            if tag != lastBg { print("backdrop", tag, "from frame", index); lastBg = tag }
        }
        var colour = Data(count: halfW * halfH * 4), core = Data(count: halfW * halfH)
        colour.withUnsafeMutableBytes { (raw: UnsafeMutableRawBufferPointer) in
            let d = raw.bindMemory(to: UInt8.self).baseAddress!
            for y in 0..<halfH { memcpy(d + y * halfW * 4, cp + y * crow, halfW * 4) }
        }
        core.withUnsafeMutableBytes { (raw: UnsafeMutableRawBufferPointer) in
            let d = raw.bindMemory(to: UInt8.self).baseAddress!
            for y in 0..<halfH { for x in 0..<halfW {
                d[y * halfW + x] = mp == nil ? 0 : mp![y * mrow + x * 4 + 1]
            } }
        }
        CVPixelBufferUnlockBaseAddress(color, [])
        if let m = maskPB { CVPixelBufferUnlockBaseAddress(m, []) }
        colours.append(colour); cores.append(core); backdrops.append((bgR, bgG, bgB))
        index += 1
    }
}

// Second pass: alpha from the soft mask averaged over +-2 frames (kills single-frame flicker).
var frames: [Data] = []   // BGRA, outW x outH
let avgRadius = 2
for frame in 0..<colours.count {
    let lo = max(0, frame - avgRadius), hi = min(colours.count - 1, frame + avgRadius)
    var sum = [Int32](repeating: 0, count: halfW * halfH)
    for f in lo...hi {
        cores[f].withUnsafeBytes { (raw: UnsafeRawBufferPointer) in
            let c = raw.bindMemory(to: UInt8.self).baseAddress!
            for i in 0..<(halfW * halfH) { sum[i] += Int32(c[i]) }
        }
    }
    let own = [UInt8](cores[frame])
    let n = Double(hi - lo + 1)
    var out = Data(count: outW * outH * 4)
    out.withUnsafeMutableBytes { (raw: UnsafeMutableRawBufferPointer) in
        let o = raw.bindMemory(to: UInt8.self).baseAddress!
        colours[frame].withUnsafeBytes { (src: UnsafeRawBufferPointer) in
            let cp = src.bindMemory(to: UInt8.self).baseAddress!
            for y in 0..<halfH {
                for x in 0..<halfW {
                    let i = y * halfW + x
                    // Mostly this frame's mask, steadied by the window average.
                    let m = (0.6 * Double(own[i]) + 0.4 * Double(sum[i]) / n) / 255
                    let a = max(0, min(1, (m - 0.25) / 0.5))
                    let c = cp + i * 4
                    let left = o + (y * outW + x) * 4, right = o + (y * outW + x + halfW) * 4
                    left[0] = c[0]; left[1] = c[1]; left[2] = c[2]; left[3] = 255
                    let av = UInt8((a * 255).rounded())
                    right[0] = av; right[1] = av; right[2] = av; right[3] = 255
                }
            }
        }
    }
    colours[frame] = Data()
    frames.append(out)
    if frame == 0, let path = previewPath {
        // straight RGBA preview of frame 0 for checking the matte
        var rgba = Data(count: halfW * halfH * 4)
        rgba.withUnsafeMutableBytes { (dst: UnsafeMutableRawBufferPointer) in
            out.withUnsafeBytes { (srcB: UnsafeRawBufferPointer) in
                let d = dst.bindMemory(to: UInt8.self).baseAddress!, s = srcB.bindMemory(to: UInt8.self).baseAddress!
                for y in 0..<halfH { for x in 0..<halfW {
                    let l = s + (y * outW + x) * 4, rr = s + (y * outW + x + halfW) * 4, t = d + (y * halfW + x) * 4
                    t[0] = l[2]; t[1] = l[1]; t[2] = l[0]; t[3] = rr[0]
                } }
            }
        }
        let provider = CGDataProvider(data: rgba as CFData)!
        let cg = CGImage(width: halfW, height: halfH, bitsPerComponent: 8, bitsPerPixel: 32, bytesPerRow: halfW * 4,
                         space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGBitmapInfo(rawValue: CGImageAlphaInfo.last.rawValue),
                         provider: provider, decode: nil, shouldInterpolate: false, intent: .defaultIntent)!
        let dest = CGImageDestinationCreateWithURL(URL(fileURLWithPath: path) as CFURL, "public.png" as CFString, 1, nil)!
        CGImageDestinationAddImage(dest, cg, nil); CGImageDestinationFinalize(dest)
    }
}
print("frames", frames.count, "fps", fps, "out", outW, "x", outH)

// ping-pong: 0..n-1 then n-2..1
var order = Array(0..<frames.count)
// 2026-11-07: NO_PINGPONG=1 writes the clip forward only (a run cycle played backwards looks wrong).
if frames.count > 2 && ProcessInfo.processInfo.environment["NO_PINGPONG"] != "1" { order += Array((1..<(frames.count - 1)).reversed()) }

if rawOut {
    for f in order { FileHandle.standardOutput.write(frames[f]) }
    print("wrote raw frames", order.count, outW, "x", outH)
    exit(0)
}

try? FileManager.default.removeItem(at: output)
let writer = try AVAssetWriter(outputURL: output, fileType: .mp4)
let settings: [String: Any] = [
    AVVideoCodecKey: AVVideoCodecType.h264, AVVideoWidthKey: outW, AVVideoHeightKey: outH,
    AVVideoCompressionPropertiesKey: [AVVideoAverageBitRateKey: requestedMbps.map { Int($0 * 1_000_000) } ?? max(12_000_000, Int(Double(outW * outH) * Double(fps) * 0.18)), AVVideoProfileLevelKey: AVVideoProfileLevelH264HighAutoLevel,
                                      AVVideoMaxKeyFrameIntervalKey: 30],
]
let wInput = AVAssetWriterInput(mediaType: .video, outputSettings: settings)
wInput.expectsMediaDataInRealTime = false
let adaptor = AVAssetWriterInputPixelBufferAdaptor(assetWriterInput: wInput, sourcePixelBufferAttributes: [
    kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA, kCVPixelBufferWidthKey as String: outW, kCVPixelBufferHeightKey as String: outH])
writer.add(wInput)
writer.startWriting()
writer.startSession(atSourceTime: .zero)
let timescale: Int32 = 600
for (k, f) in order.enumerated() {
    while !wInput.isReadyForMoreMediaData { usleep(2000) }
    var pb: CVPixelBuffer?
    CVPixelBufferPoolCreatePixelBuffer(nil, adaptor.pixelBufferPool!, &pb)
    let (p, row) = bytes(pb!)
    frames[f].withUnsafeBytes { (s: UnsafeRawBufferPointer) in
        let sp = s.bindMemory(to: UInt8.self).baseAddress!
        for y in 0..<outH { memcpy(p + y * row, sp + y * outW * 4, outW * 4) }
    }
    CVPixelBufferUnlockBaseAddress(pb!, [])
    adaptor.append(pb!, withPresentationTime: CMTime(value: CMTimeValue(Double(k) * Double(timescale) / Double(fps)), timescale: timescale))
}
wInput.markAsFinished()
let done = DispatchSemaphore(value: 0)
writer.finishWriting { done.signal() }
done.wait()
print("wrote", output.path, "frames", order.count, "status", writer.status.rawValue, writer.error?.localizedDescription ?? "")
if writer.status != .completed { exit(1) }
