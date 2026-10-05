import { createHash } from "node:crypto";
import { existsSync, lstatSync, readFileSync, readdirSync, statSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const requireFile = path => {
  if (!existsSync(path) || lstatSync(path).isSymbolicLink() || !statSync(path).isFile()) {
    throw new Error(`缺少 WebGL 交付文件：${path}`);
  }
};

const indexPath = join(root, "index.html");
requireFile(indexPath);
requireFile(join(root, ".nojekyll"));

const html = readFileSync(indexPath, "utf8");
// Every release shows its number on the loading screen; it must match release.json.
const release = JSON.parse(readFileSync(join(root, "release.json"), "utf8"));
const versionLabel = `v${release.version}`;
const packageVersion = JSON.parse(readFileSync(join(root, "package.json"), "utf8")).version;
if (!/^\d+\.\d+\.\d+$/.test(release.version) || !html.includes(`>${versionLabel}<`) ||
    !html.includes(`productVersion: "${release.version}"`) || packageVersion !== release.version) {
  throw new Error(`版本号不一致：加载页、productVersion、package.json 应与 release.json 的 ${versionLabel} 相同`);
}
if (!/<canvas[^>]+width="720"[^>]+height="1536"/.test(html)) {
  throw new Error("WebGL 画布不是 720×1536 竖屏尺寸");
}
if (!/--portrait-ratio:\s*720\s*\/\s*1536/.test(html)) {
  throw new Error("WebGL 网页外壳比例与 720×1536 画布不一致");
}
if (!html.includes('new URL("Build/", pageUrl)')) {
  throw new Error("WebGL 资源没有使用 GitHub Pages 子路径安全地址");
}
if (!html.includes('new URL("service-worker.js", pageUrl)') ||
    !html.includes("navigator.serviceWorker.getRegistrations")) {
  throw new Error("缺少资源缓存 Service Worker 的注册或重新加载时的清理逻辑");
}
for (const asset of ["home-stage.css", "home-stage.js", "character-moments.css",
  "character-moments.js", "lipstick-cursor.svg", "manifest.webmanifest", "favicon.svg",
  "icons/apple-touch-icon.png", "media/loading-stage.webp", "audio-skin.js"]) {
  if (html.includes(`./${asset}`)) requireFile(join(root, asset));
}
const webManifest = JSON.parse(readFileSync(join(root, "manifest.webmanifest"), "utf8"));
for (const icon of webManifest.icons) requireFile(join(root, icon.src));
if (!html.includes('id="character-moment"') ||
    !html.includes('./character-moments.js') || !html.includes('./character-moments.css')) {
  throw new Error("首页缺少角色演出层");
}
if (html.includes('moment-controls') || html.includes('data-moment=')) {
  throw new Error("角色点击不得覆盖额外的演出菜单或按钮");
}
const momentCss = readFileSync(join(root, 'character-moments.css'), 'utf8');
const momentJs = readFileSync(join(root, 'character-moments.js'), 'utf8');
if (momentCss.includes('mask-image') || !momentCss.includes('opacity: 0') ||
    !momentJs.includes('uploadFrame') || !momentJs.includes('texSubImage2D')) {
  throw new Error("角色必须在 Unity 真实 UI 层级绘制，网页仅解码视频");
}
// Composed music, UI sounds and clip voices referenced by audio-skin.js.
const audioSkin = readFileSync(join(root, 'audio-skin.js'), 'utf8');
for (const file of ['lobby-theme.mp3', 'ui-click.wav', 'ui-success.wav']) {
  if (!audioSkin.includes(`'${file}'`)) throw new Error('audio-skin.js 缺少音频：' + file);
  requireFile(join(root, 'media', file));
}
for (const name of ['catalena-look', 'catalena-whisper', 'catalena-live']) requireFile(join(root, 'media', `${name}.voice.mp3`));
const inkRoot = join(root, 'StreamingAssets', 'AlbumInkR02');
const ink = JSON.parse(readFileSync(join(inkRoot, 'manifest.json'), 'utf8'));
if (ink.frames !== 48 || ink.fps !== 30 || ink.sheets.length !== 4) throw new Error('油墨动画配置不符');
for (const sheet of ink.sheets) {
  if (!/^ink-flow-[0-3]\.png$/.test(sheet.file)) throw new Error('油墨图集路径无效');
  const bytes = readFileSync(join(inkRoot, sheet.file));
  if (createHash('sha256').update(bytes).digest('hex') !== sheet.sha256 ||
      bytes.readUInt32BE(16) !== 2048 || bytes.readUInt32BE(20) !== 1440 || bytes[25] !== 6) {
    throw new Error('油墨图集不匹配或缺少真实 RGBA: ' + sheet.file);
  }
}
// Battle assets stay outside the Unity archive: verify every referenced native RGBA
// component and the six independent dice before allowing a Pages deployment.
const battleRoot = join(root, 'StreamingAssets', 'BattlePsd928');
const battleManifest = JSON.parse(readFileSync(join(battleRoot, 'manifest.json'), 'utf8'));
if (battleManifest.width !== 2946 || battleManifest.height !== 6144 ||
    battleManifest.scaleMode !== 'contain' || battleManifest.entries.length !== 44 ||
    battleManifest.sourceSha256 !== 'c2a83aa1960de149268364118eb6f86a9d5af7b83b8119a5c70a3748ed4864f1') {
  throw new Error('战斗资源不是最新六骰 PSD');
}
const battleKeys = new Set();
for (const entry of battleManifest.entries) {
  if (!/^[a-f0-9]{16}-[a-z0-9-]+\.png$/.test(entry.file) || battleKeys.has(entry.key))
    throw new Error('战斗资源路径或键重复');
  battleKeys.add(entry.key);
  const bytes = readFileSync(join(battleRoot, entry.file));
  if (!entry.file.startsWith(createHash('sha256').update(bytes).digest('hex').slice(0, 16) + '-') ||
      bytes.subarray(0, 8).toString('hex') !== '89504e470d0a1a0a' || bytes[25] !== 6 ||
      entry.width <= 0 || entry.height <= 0) throw new Error('战斗分层图哈希或 RGBA 无效：' + entry.file);
}
for (let i = 1; i <= 6; i++) if (!battleKeys.has('die-' + i)) throw new Error('缺少第 ' + i + ' 颗骰子');
for (const name of ["catalena-look", "catalena-whisper", "catalena-live"]) {
  const clip = join(root, "media", `${name}.webm`);
  requireFile(clip);
  const bytes = readFileSync(clip);
  if (statSync(clip).size < 1000 ||
      statSync(clip).size >= 100 * 1024 * 1024 ||
      bytes.subarray(0, 4).toString("hex") !== "1a45dfa3" ||
      bytes.indexOf(Buffer.from([0x53, 0xc0, 0x81, 0x01])) < 0) {
    throw new Error(`透明角色演出文件无效：${name}`);
  }
  // Safari/iOS copies: side-by-side colour|alpha built by scripts/build-packed-video.sh,
  // VP9 WebM first and HEVC MP4 as the fallback.
  const packedWebm = join(root, "media", `${name}.packed.webm`);
  requireFile(packedWebm);
  const packedWebmBytes = readFileSync(packedWebm);
  if (packedWebmBytes.length < 1000 || packedWebmBytes.length >= 100 * 1024 * 1024 ||
      packedWebmBytes.subarray(0, 4).toString("hex") !== "1a45dfa3" || packedWebmBytes.indexOf(Buffer.from("V_VP9")) < 0) {
    throw new Error(`iPhone 透明角色演出文件无效：${name}.packed.webm`);
  }
  const packed = join(root, "media", `${name}.packed.mp4`);
  requireFile(packed);
  const packedBytes = readFileSync(packed);
  if (packedBytes.length < 1000 || packedBytes.length >= 100 * 1024 * 1024 ||
      packedBytes.subarray(4, 8).toString("latin1") !== "ftyp" || packedBytes.indexOf(Buffer.from("hvc1")) < 0) {
    throw new Error(`iPhone 透明角色演出文件无效：${name}`);
  }
}

const references = [...html.matchAll(/buildAssetUrl\("([^"]+\.(?:data\.unityweb|framework\.js\.unityweb|wasm\.unityweb|loader\.js))"\)/g)]
  .map(match => match[1]);
if (references.length !== 4) {
  throw new Error(`WebGL 应引用 4 个构建文件，实际为 ${references.length} 个`);
}
if (new Set(references).size !== 4) {
  throw new Error("WebGL 构建文件引用存在重复");
}

requireFile(join(root, "build-versions.json"));
const versions = JSON.parse(readFileSync(join(root, "build-versions.json"), "utf8"));
if (versions.schemaVersion !== 1 || !Array.isArray(versions.current) ||
    [...versions.current].sort().join() !== [...references].sort().join()) {
  throw new Error("版本清单与当前 HTML 的四个资源不一致");
}
const previous = versions.previous;
if (!Array.isArray(previous) || (previous.length !== 0 && previous.length !== 4) ||
    new Set(previous).size !== previous.length) {
  throw new Error("上一版资源清单不完整");
}
if (!html.includes('new URL("build-versions.json", pageUrl)') || !html.includes('cache: "no-store"')) {
  throw new Error("缺少启动前的无缓存版本检查");
}

const unityAssetPattern = /^[0-9a-f]{32}\.(?:data\.unityweb|framework\.js\.unityweb|wasm\.unityweb|loader\.js)$/;
const expectedSuffixes = [".data.unityweb", ".framework.js.unityweb", ".wasm.unityweb", ".loader.js"];
for (const suffix of expectedSuffixes) {
  if (references.filter(file => file.endsWith(suffix)).length !== 1) {
    throw new Error(`WebGL 应且仅应引用一个 *${suffix} 文件`);
  }
  if (previous.length && previous.filter(file => typeof file === 'string' && file.endsWith(suffix)).length !== 1) {
    throw new Error(`上一版应且仅应保留一个 *${suffix} 文件`);
  }
}

const githubFileLimit = 100 * 1024 * 1024;
const buildDirectory = join(root, "Build");
const keptFiles = [...new Set([...references, ...previous])];
const assets = keptFiles.map(file => {
  if (!unityAssetPattern.test(file)) {
    throw new Error(`WebGL 文件未使用内容哈希命名：${file}`);
  }
  const path = join(buildDirectory, file);
  requireFile(path);
  const bytes = statSync(path).size;
  if (bytes >= githubFileLimit) {
    throw new Error(`WebGL 文件达到或超过 GitHub 100 MiB 限制：${file} (${bytes} bytes)`);
  }
  const sha256 = createHash("sha256").update(readFileSync(path)).digest("hex");
  if (!file.startsWith(sha256.slice(0, 32) + ".")) {
    throw new Error(`WebGL 文件内容与哈希文件名不符，可能混入旧版资源：${file}`);
  }
  return { file, bytes, sha256 };
});

const buildEntries = readdirSync(buildDirectory, { withFileTypes: true });
const unexpectedBuildEntries = buildEntries.filter(entry =>
  !entry.isFile() || !keptFiles.includes(entry.name)
);
if (unexpectedBuildEntries.length > 0) {
  throw new Error(`Build 目录包含 index.html 未引用的内容：${unexpectedBuildEntries.map(entry => entry.name).join(", ")}`);
}
if (buildEntries.length !== keptFiles.length) {
  throw new Error(`Build 目录应仅包含本版及上一版资源，实际为 ${buildEntries.length} 个`);
}

const lobbyVideoPath = join(root, "StreamingAssets", "Lobby", "lobby-loop.mp4");
requireFile(lobbyVideoPath);
if (statSync(lobbyVideoPath).size < 1024) {
  throw new Error("大厅循环视频内容异常或为空");
}
const lobbyVideo = {
  file: "StreamingAssets/Lobby/lobby-loop.mp4",
  bytes: statSync(lobbyVideoPath).size,
  sha256: createHash("sha256").update(readFileSync(lobbyVideoPath)).digest("hex"),
};

console.log(JSON.stringify({ success: true, canvas: "720x1536", githubFileLimit, lobbyVideo,
  current: references, previous, assets }, null, 2));
