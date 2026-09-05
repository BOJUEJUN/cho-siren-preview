# WebGL 缓存与发布保护

## 已确认原因和边界

2026-09-05 在 Pages `f511fd4` 上核对：当前 WASM 返回 200，上版 `e0249fd` 的 WASM 返回 404。将上版 HTML 作为浏览器缓存重放时，旧 loader 请求也为 404，界面停在重新加载提示。由此确认“删掉旧哈希文件 + 浏览器仍持有旧入口”是一条可复现的失败路径；它不能证明用户之前每一次 iPhone 错误都来自这一原因。

## 当前保护

- `build-versions.json` 记录 `schemaVersion: 1`、完整 `current` 四资源和 `previous` 四资源。当前游戏仍为源码 `6e0f1a2` 的已验证二进制，本批不改 C#、游戏数据、UI 布局或存档。
- 启动前对版本清单进行小体积、无缓存请求（带请求时间参数，5 秒超时）。只接受四个合法内容哈希文件名及唯一的数据/框架/WASM/loader 后缀；整组切换，禁止新旧混装。清单缺失、非法或超时则使用 HTML 内的整组引用，不循环跳转。
- 发布保留完整上一版资源。仅更新 HTML 而二进制不变时，不轮换/丢弃上一版。首次部署恢复上一版 `e0249fd` 的四个已验证文件作为兼容缓冲；不恢复上版 UI 为当前版。
- 加载错误保持重试入口，不会被后续普通警告或旧定时器隐藏。长错误文案在小屏内换行、滚动。重试保留网站路径及其他查询参数，新增缓存穿透参数。
- 仅注销当前项目的 Service Worker；旧退役 worker 只清理 `cho-siren-` 缓存并刷新本游戏页面，不刷新同域其他项目。

## 发布工具

macOS/Windows 共用 `Tools/Stage-WebGL.mjs`（Node 20+）。PowerShell 入口 `Tools/Publish-WebGLToPages.ps1` 仅包装它，不再维护第二套会删除上一版资源的实现。

先运行 dry-run（不加 `--apply`）：

```sh
node Tools/Stage-WebGL.mjs --build /absolute/verified-build --pages /absolute/pages-checkout
```

确认输出后增加 `--apply`。首次补回兼容版可使用 `--fallback-build /absolute/previous-verified-build`，仅在目标无清单且当前二进制未更换时允许。工具不 commit、不 push。保留用户未跟踪文件，拒绝软链接、路径相交、未知 Build 文件、哈希不符、不完整历史版本和文件/目录冲突；删除范围仅为清单确认的过期哈希文件。

`Tools/Test-WebGLDeliverable.ps1` 的四文件限制仍用于单个原始 Unity 构建，不用于已有当前/上一版资源的 Pages 目录。Pages 验证由 `scripts/verify-webgl.mjs` 对八文件的明确清单及内容哈希进行检查。`npm run check` 包含加载器回归测试。

## 验证记录

- `node --test Tools/test-stage-webgl.mjs`：5/5，覆盖 A→B→C 轮换、同版幂等、dry-run、首次恢复、危险路径/未知文件/缺损资源拦截。
- Pages `node --test scripts/test-loader.mjs`：12/12，覆盖旧入口选择整组新版、离线/缺失/超时/畸形清单、安全 URL、保留错误提示和可重试、服务工作线程隔离。
- 隔离 Chrome 320×568 三条实际 WebGL 路径通过：未带新保护的旧 HTML 加载保留的旧整包；含保护但内嵌旧引用的 HTML 加载当前整包；人为阻断当前 WASM 后，提示按钮可重新加载进入游戏。前两条无 pageerror；人为阻断时 Unity 抛出 `Cannot read properties of undefined (reading 'instance')`，重试后成功，不能将该强制失败测试宣称全程无异常。
- 截图 `Artifacts/qa-20260905/loader-stale-before.png`、`loader-legacy-success.png`、`loader-cached-guard-success.png`、`loader-friendly-error.png`、`loader-retry-success.png`。
- PowerShell 包装入口未在 Windows 实跑；共享 Node 核心已在本机执行并真实准备发布。iPhone Safari 真机仍未验证。仅保留上一版，不保证无限旧版本可加载；更早错误入口应重新加载最新网页。
- 线上部署和内容哈希须核对后追加，不以本地测试冒充已上线。
