import { createHash } from "node:crypto";
import { existsSync, readFileSync, statSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const requireFile = path => {
  if (!existsSync(path) || !statSync(path).isFile()) {
    throw new Error(`缺少 WebGL 交付文件：${path}`);
  }
};

const indexPath = join(root, "index.html");
requireFile(indexPath);
requireFile(join(root, ".nojekyll"));

const html = readFileSync(indexPath, "utf8");
if (!/<canvas[^>]+width="720"[^>]+height="1536"/.test(html)) {
  throw new Error("WebGL 画布不是 720×1536 竖屏尺寸");
}
if (!/--portrait-ratio:\s*720\s*\/\s*1536/.test(html) || html.includes("1552")) {
  throw new Error("WebGL 网页外壳比例与 720×1536 画布不一致");
}
if (!html.includes('new URL("Build/", pageUrl)')) {
  throw new Error("WebGL 资源没有使用 GitHub Pages 子路径安全地址");
}
if (!html.includes("navigator.serviceWorker.getRegistrations")) {
  throw new Error("缺少旧 Service Worker 注销逻辑");
}

const references = [...html.matchAll(/buildAssetUrl\("([^"]+\.(?:data\.unityweb|framework\.js\.unityweb|wasm\.unityweb|loader\.js))"\)/g)]
  .map(match => match[1]);
if (references.length !== 4) {
  throw new Error(`WebGL 应引用 4 个构建文件，实际为 ${references.length} 个`);
}

const assets = references.map(file => {
  if (!/^[0-9a-f]{32}\./.test(file)) {
    throw new Error(`WebGL 文件未使用内容哈希命名：${file}`);
  }
  const path = join(root, "Build", file);
  requireFile(path);
  const bytes = statSync(path).size;
  const sha256 = createHash("sha256").update(readFileSync(path)).digest("hex");
  return { file, bytes, sha256 };
});

const lobbyVideoPath = join(root, "StreamingAssets", "Lobby", "lobby-loop.mp4");
requireFile(lobbyVideoPath);
const lobbyVideo = {
  file: "StreamingAssets/Lobby/lobby-loop.mp4",
  bytes: statSync(lobbyVideoPath).size,
  sha256: createHash("sha256").update(readFileSync(lobbyVideoPath)).digest("hex"),
};

console.log(JSON.stringify({ success: true, canvas: "720x1536", lobbyVideo, assets }, null, 2));
