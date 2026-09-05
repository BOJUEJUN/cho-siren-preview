import { createHash } from "node:crypto";
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
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
if (!/--portrait-ratio:\s*720\s*\/\s*1536/.test(html)) {
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
if (new Set(references).size !== 4) {
  throw new Error("WebGL 构建文件引用存在重复");
}

const unityAssetPattern = /^[0-9a-f]{32}\.(?:data\.unityweb|framework\.js\.unityweb|wasm\.unityweb|loader\.js)$/;
const expectedSuffixes = [".data.unityweb", ".framework.js.unityweb", ".wasm.unityweb", ".loader.js"];
for (const suffix of expectedSuffixes) {
  if (references.filter(file => file.endsWith(suffix)).length !== 1) {
    throw new Error(`WebGL 应且仅应引用一个 *${suffix} 文件`);
  }
}

const githubFileLimit = 100 * 1024 * 1024;
const buildDirectory = join(root, "Build");
const assets = references.map(file => {
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
  !entry.isFile() || !references.includes(entry.name)
);
if (unexpectedBuildEntries.length > 0) {
  throw new Error(`Build 目录包含 index.html 未引用的内容：${unexpectedBuildEntries.map(entry => entry.name).join(", ")}`);
}
if (buildEntries.length !== references.length) {
  throw new Error(`Build 目录应仅包含本版 4 个引用文件，实际为 ${buildEntries.length} 个`);
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

console.log(JSON.stringify({ success: true, canvas: "720x1536", githubFileLimit, lobbyVideo, assets }, null, 2));
