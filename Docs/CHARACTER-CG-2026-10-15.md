# CHO-SIREN R64：新版角色与CG配乐

版本 v0.3.69 / `PSD-20261015-CHARACTER-CG-R64`。已上线，提交 `1ce9d318f762c59e73c3fc36821a09a333b5474f`。首页纠正后29项PlayMode、83项网页检查通过；最终包浏览器、模拟器及正式线上验收完成。

用户明确禁止 Devin，且不再续费。不得调用、检查或管理 Devin；本轮由 Codex 直接实现。该指令高于旧技能中的委派要求，源码 AGENTS.md 已同步。

## 新版成员立绘，首页保留原来两张

用户明确：这些新版成员图不能用作首页角色。首页仅有用户之前单独设计的两张；有新增首页素材用户会另行提供，禁止自动替换或扩充。首页的Carousel、Motion、ChoSirenAppPsdHome、GameModel及原切换测试已按修改前备份逐字节恢复；核对见 `homepage-restoration.json`。此前误换首页的本地候选未提交、未上传，已撤下。

以用户提供的 `魅族.psd` 为唯一角色视觉来源，SHA256 `b826238790ff59e34b92240f6ab8eb904bdb00c803fa30add15b47eb0ee78a09`，画布8334×4625。PSD包含星璃、绯音、雾白、夜莺四人，分别对应原成员ID；未改成员目录、等级、装备、战斗数值或存档结构。

运行版仅替换四组成员全身立绘与头像，共8张原生RGBA。排除独立灰底图层；夜莺额外合成原PSD麦克风补片。未生成、重画、按白色抠图或修改像素透明度。原PSD、完整原始图层和全分辨率人物合成保存在任务工作目录。新增描边的9个资源及meta已从项目删除，原始描边层保留在本地母版。

运行版保留主体不透明边界外64px保护；裁掉PSD中远离主体的低透明度残留，原始像素完整保存在母版。具体全局裁剪、头像窗口、尺寸、alpha统计及SHA见 `CHARACTER-CG-EVIDENCE-2026-10-15/character-manifest.json`，其中描边记录标明非运行资源。全身图高度2048，头像512×768；Unity通过实际导入API设置FullRect/原生alpha/WebGL RGBA32。

首页保持原来两位人物、原素材、对话、动作和演出视频，首页选择仍是0/1。成员、团队和装备页继续读取原成员资源路径，八个现有立绘/头像GUID保持。新版成员图与首页设计相互独立。

## 音乐优先级

加载CG原文件含10.08秒H.264视频和32kHz双声道AAC，文件SHA256 `5306f9c2f18ead85c014a02c3f5c6a83f1fdb33df0ba2cd5ada3fbcfa77db1f8`。恢复其自带配乐，音量0.45。Unity全局Neon Moonwalk音频文件保持原字节，常规菜单和演出继续使用它。

`GameAudio`按媒体所有者暂停全局音源，最后一个媒体结束再续播；浏览器以原子列表同步所有者，原生加载CG独立持有。关闭音乐不会释放CG所有权而露出全局配乐；视频失败、退出或停用先静音/停止再释放。背景暂停、浏览器返回缓存、手势受限、失败重试均有覆盖。音乐设置未从Unity确认前，CG保持静音；用户明确选择或已保存设置确认后才能启声。音乐和音效独立。

NativeTitleScreen采用Direct音频轨，并在实际prepareCompleted后重新应用音量/静音；原生准备超时使用海报并释放音乐。现有其他视频入口仅LobbyVideoLoopPlayer提供配乐；语音角色片段沿用压低全局音量的语音逻辑。

## 验收证据

- Unity 6000.6.0f1：首页恢复后7项真实BGM/CG音源测试、22项首页检查，共29项通过，见 `playmode-r64-home-restored.xml`。首页命中检查使用实际720×1536竖屏画布；共享导航/顶栏依照现行界面断言。
- 正式网页检查83/83：加载器39、角色视频生命周期27、音频桥接8、加载CG9；完整新/旧八个引擎资源哈希和GitHub文件大小限制通过。
- 真实本地WebGL：390×844、390×690，新版成员头像、雾白透明装备页与底部按钮通过。标题CG独占时全局循环音源为0；退出后CG静音停止、全局仅一个活动循环音源，实际输出有信号；音乐关闭时输出RMS为0。最终包已在浏览器和模拟器确认原首页第一张→第二张→第一张，原动作/演出保持；截图 `home-original-01/02-final-*`。原首页PNG与正式流式资源SHA一致，见 `homepage-identity.json`。
- iOS27 / iPhone18 Pro模拟器Safari：成员页、雾白详情与全部装备底部按钮可见。成员截图在 `captures/`；最终两角色首页实际切换、回环截图已保存。这是模拟器交互证据，不代表物理手机声学验收或iOS安装包验收。

## 源码、构建和回滚

源码 `/Users/bojuejun/cho-siren-unity`。复用隔离工程 `/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/ui-unification-20261013/project` 编译和验证，再以修改前SHA核对，仅同步30个成员/音乐/导入/测试/规则相关文件；首页相关改动另行恢复。保留所有无关脏工作。证据 `source-integration-manifest.json` 与 `homepage-restoration.json`。最终构建日志 `build-r64-home-restored.log`；不得交付早期误换四角色首页的构建。

构建入口 `ChoSiren.Editor.ChoSirenCharacterCgBuild.BuildWebGLRelease`。最终数据大小和哈希以发布验收记录为准，须低于GitHub单文件104,857,600限制。正式Stage工具先dry-run后apply，保留完整R63上一版四资源，仅轮换清单中的过期R62资源。网页历史记录 `releases/R63.json`，新备份分支 `backup/v0.3.69`；不强推、不清档。

工作目录 `/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/character-cg-20261015` 包含原PSD备份、母版、导出脚本、原文件备份、导入/测试/构建日志、本地运行音频记录及截图。生产页没有本地QA探针或跳过协议的测试代码。

## 正式上线验收

公开游戏：https://bojuejun.github.io/cho-siren-preview/?v=0.3.69 。main和backup/v0.3.69均指向 `1ce9d318f762c59e73c3fc36821a09a333b5474f`，网页仓库干净。

同一提交的Quality checks与Pages均success，见 `github-actions-r64.json`。当前data使用8段HTTP206完整重建，102,063,756字节，SHA256 `d491199586e3e8ea805a054827c39cc9f427169aef2a36a59577c72320bf744d` 与本地完全一致；其余引擎文件、网页脚本、版本清单和原首页两人的四张PNG也通过完整哈希核对。R63四件旧引擎文件远端HEAD200且长度匹配，只记录可用性，不作为新一轮完整远端哈希验收。

正式网页实际操作：原首页第一位→第二位→第一位，确认保留原图；四个新版成员头像可见，雾白详情立绘透明、装备区和底部控制正常。标题CG实播时readyState=4、muted=false、paused=false；退出后muted=true、paused=true、src已卸载。音乐偏好由false临时开启验收后已恢复false，原首页选择恢复0。当前正式运行无新增控制台错误，见 `public-runtime-r64.json` 与 `captures/*-public.png`。

最终保留正式首页标签，浏览器临时手机视口已恢复默认。模拟器已实际验收原两张首页与成员替换；物理手机声音及iOS安装包仍未验证。
