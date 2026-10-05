# CHO-SIREN 网页预览仓库

本仓库就是 GitHub Pages 上线的网页版（https://bojuejun.github.io/cho-siren-preview/），`main` 分支即线上版本，推送后 Pages 自动发布。Unity 工程在别的仓库；`index.html`、`character-moments.js`、`home-stage.*`、`media/` 来自 Unity 工程的 `Assets/WebGLTemplates/ChoSirenPortrait/`，改动后要同步回去（`patches/unity-template-since-R17.patch` 是自 R17 起的累计补丁；新增的 `audio-skin.js`、`media/*.packed.mp4`、`media/*.voice.mp3`、`media/lobby-theme.mp3`、`media/ui-*.wav`、`media/loading-stage.webp` 要一并复制）。

声音：`audio-skin.js` 在运行时把 Unity 生成的占位音乐和音效换成 `scripts/audio/compose_lobby_theme.py` 渲染的版本，开关和音量仍归 Unity 管。Unity 里加入正式配乐（`Assets/Resources/Audio/bgm.*`）后，占位规格不再出现，替换自动失效。

## 上线流程（用户已授权：改动验证通过后直接上线，不必逐次确认）

1. 运行 `npm run check`，必须全部通过。
2. 定版本号：每次上线把 `version` 的末位加一（0.3.9 → 0.3.10），重大版本由用户决定。`releaseId` 继续沿用内部发布编号 `R<数字>`：同一个 Unity 包上的网页层改动加字母后缀（R17D、R17E…），新的 Unity 构建用下一个数字。
3. 写记录：
   - 把当前 `release.json` 移到 `releases/<当前发布编号>.json`，再写新的 `release.json`（字段沿用现有格式，`previousWebCommit`、`previousBackupBranch` 指向上一版）。
   - `RELEASES.md` 顶部加一条：改了什么、怎么验证的。
   - 加载页右下角的版本号（`#loading-version`）改成 `v<version>`，`index.html` 的 `productVersion` 和 `package.json` 的 `version` 同步改。`npm run check` 会核对四处一致。
4. 提交到工作分支，快进合并到 `main`：`git push origin <工作分支>:main`。**`main` 要单独一条命令推送**：和其它分支放在同一条 `git push` 里时，GitHub Pages 曾经不触发发布（R17C、v0.3.9）。
5. 备份：给新上线的提交建分支 `backup/v<version>`：`git push origin <commit>:refs/heads/backup/v<version>`。每个上线版本都有自己的 backup 分支（R17、R17A–R17C 之前的命名是 `backup/R17*`）；本环境不能推送 Git 标签，所以用分支。
6. 确认 GitHub Actions 的「Quality checks」成功，并且「pages build and deployment」里有这个提交的运行且成功（只看 Quality checks 不够），再告诉用户上线的版本号。

## 回退

不改写历史：把目标版本的文件原样恢复，作为一次新提交上线。例如回到 R17（v0.3.8 线上版）：

```
git fetch origin backup/R17
git rm -r -q . && git checkout origin/backup/R17 -- . && git commit -m "Roll back to R17"
git push origin HEAD:main
```

回退本身也是一次上线：照常升版本号、写记录、建备份分支。
