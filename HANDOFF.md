# CHO-SIREN 交接文档（网页预览 → 本地 Unity 工程）

更新：2026-10-08。写给接手 Unity 工程的本地会话。网页预览仓库是 `BOJUEJUN/cho-siren-preview`，
线上地址 https://bojuejun.github.io/cho-siren-preview/ ，`main` 即线上。

## 1. 现状

- 线上：**v0.3.16**（提交 8a851db），Unity 包仍是 **R17**（PSD-20260929-HOME-R17，源码提交 cb77657，
  2026-09-29 出包）。R17 之后的 16 个版本（R17A–R17K / v0.3.9–v0.3.16）**全是网页层改动**，没有重新出过 Unity 包。
- 每个上线版本都有备份分支：`backup/R17`（8ac56ef，R17 原样）、`backup/R17A`–`backup/R17C`、
  `backup/v0.3.9` … `backup/v0.3.16`。回退方法见仓库 `CLAUDE.md`。
- 改了什么、怎么验证的：仓库 `RELEASES.md` 顶部逐版记录；每版的结构化记录在 `release.json` / `releases/*.json`。
- 都没有在实体 iPhone 上验证过，验证全部来自本地 Chromium 模拟（含 iPhone 模式和触摸）。

## 2. R17 之后网页层做了什么（出新包时不能丢）

| 功能 | 文件 |
|---|---|
| Service Worker 缓存带哈希的游戏文件（`Build/<sha>.*`、`StreamingAssets/**/<sha16>-*.png`），再次打开不重新下载 | `service-worker.js`、`index.html` |
| 只有一个加载页，盖住 Unity 启动画面和 Unity 自己的加载场景，等大厅 `window.choSirenStage.active` 为真才消失 | `index.html` |
| 加载页右下角版本号 `v<版本>`、主屏幕安装（manifest、图标、apple meta）、宽屏两侧模糊背景 | `index.html`、`manifest.webmanifest`、`icons/`、`media/loading-stage.webp` |
| iPhone/Safari 角色动画透明：「颜色｜32px｜透明度」拼接视频，GPU 合成后写进 Unity 纹理；能播 WebM 的 Safari 用 VP9 WebM，否则 HEVC MP4，加载失败自动换下一个 | `character-moments.js`、`media/*.packed.webm`、`media/*.packed.mp4`、`scripts/build-packed-video.sh` |
| 点屏幕时预先解锁两个播放器（iPhone 低电量模式 / 微信内置浏览器里也能播）；切后台时结束动画；`?debug=clip` 显示动画加载日志 | `character-moments.js` |
| 原创大厅音乐和按键音：运行时把 Unity 生成的 3 个占位音（22050 Hz 单声道，长度 176400 / 1654 / 7497）换成成品，开关音量仍归 Unity；播动画时音乐压低；iPhone 拒绝有声播放时对白走 Web Audio；点屏幕唤醒音频 | `audio-skin.js`、`media/lobby-theme.mp3`、`media/ui-*.wav`、`media/*.voice.mp3`、`scripts/audio/` |
| 手机触控：无点按灰闪、无长按菜单/选字、游戏区不缩放 | `index.html`（CSS） |
| 画面被系统收回（WebGL context lost）时提示并从缓存重新载入；回到前台检查新版本并提示更新 | `index.html` |

Unity 和网页之间的接口（jslib 里已有，新包必须保留）：`ChoSirenCharacterMomentPlay / MomentStop /
UploadFrame / CurrentClip`、`ChoSirenHomeStageActive / HomeStageClick / HomeStageUiHidden`、`ChoSirenSpeakJapanese`；
网页通过 `SendMessage("PsdHome20260921", "ToggleUiFromBrowser")` 切换 UI。

## 3. 同步回 Unity 模板 `Assets/WebGLTemplates/ChoSirenPortrait/`

下次出包会用模板生成网页，**不先同步，以上改动会全部被覆盖**。

1. `index.html`、`character-moments.js`：应用网页仓库的 `patches/unity-template-since-R17.patch`（以 R17 线上版为基准的累计补丁）。
   补丁是从「出包后的网页」算出来的，模板里有 Unity 变量的行会对不上，例如构建文件名、`productVersion`
   （模板里应是 `{{{ PRODUCT_VERSION }}}`）。冲突的地方保留模板变量；加载页版本号建议写成
   `v{{{ PRODUCT_VERSION }}}`，这样只要在 Player Settings 里填版本号就行。
2. 原样复制：`audio-skin.js`、`service-worker.js`、`manifest.webmanifest`、`icons/`（4 个 png）、
   `media/` 下的 `*.packed.webm`、`*.packed.mp4`、`*.voice.mp3`、`lobby-theme.mp3`、`ui-click.wav`、`ui-success.wav`、`loading-stage.webp`。
   `home-stage.*`、`character-moments.css` 自 R17 起没改。
3. 模板里旧的 `media/catalena-*.mp4`、`catalena-*.jpg` 已不再使用，可删除。
4. 同步后先用模板出一个本地 WebGL 包，确认生成的 `index.html` 和网页仓库当前的 `index.html` 只差构建文件名和版本号。

## 4. Unity 侧待办（按优先级）

1. **Player Settings → WebGL → Splash Image 关掉 Unity 启动画面**（现在是网页加载页盖住它）。
2. **版本号**：Player Settings 的 Version 填下一个版本，即 `0.3.17`（每次上线末位加一）。
3. **首次加载太大（约 154 MB）**：Unity 数据包 94 MB + 启动时就加载的 `StreamingAssets/Reference038` 原图约 43 MB。
   可做：纹理压缩（ASTC / ETC2）、Addressables 或按页面按需加载 Reference038、代码裁剪（Managed Stripping）。
   用户要求图片保持高清，有损压缩美术图前先问用户。
4. **正式配乐**：放进 `Assets/Resources/Audio/bgm.*` 后，Unity 不再生成占位音乐，`audio-skin.js` 的替换会自动失效，不用改网页。
5. 内存：iPhone 上 WebGL 内存紧，留意 Unity 内存上限设置。

## 5. 出新包后怎么上线到网页仓库

按网页仓库 `CLAUDE.md` 的上线流程，新 Unity 包的发布编号是 **R18**。要点：

- `Build/` 里的 4 个文件按内容哈希命名：`<sha256 前 32 位>.data.unityweb / .framework.js.unityweb / .wasm.unityweb / .loader.js`；
  `Build/` 只保留本版和上一版两套，`build-versions.json` 的 `current` / `previous` 同步更新，`index.html` 里引用当前这套。
- `StreamingAssets` 里的美术图也要保持 `<sha256 前 16 位>-名字.png` 命名，缓存才不会拿到旧图。
- `npm run check` 必须全过（会核对文件哈希、版本号四处一致、拼接视频等）。
- 写 `release.json` / `releases/` / `RELEASES.md`，加载页版本号、`productVersion`、`package.json` 一致。
- **`main` 单独推送**（和别的分支一起推时 Pages 曾不发布），再建 `backup/v<版本>`，确认「Quality checks」和「pages build and deployment」都成功。

## 6. 需要用户在实体 iPhone 上确认

- 点角色：动画播放、背景透明、有对白。如果没有，打开 `?debug=clip` 截图底部日志。
- 第一次点屏幕后大厅音乐响起（静音模式下 iOS 会静音网页音乐，这是系统行为）。
- 双指 / 双击不缩放，点按不闪灰。
