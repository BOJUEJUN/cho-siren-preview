# CHO-SIREN 网页预览仓库

本仓库就是 GitHub Pages 上线的网页版（https://bojuejun.github.io/cho-siren-preview/），`main` 分支即线上版本，推送后 Pages 自动发布。Unity 工程在别的仓库；`index.html`、`character-moments.js`、`home-stage.*`、`media/` 来自 Unity 工程的 `Assets/WebGLTemplates/ChoSirenPortrait/`，改动后要同步回去（`patches/unity-template-since-R17.patch` 是自 R17 起的累计补丁）。

## 上线流程（用户已授权：改动验证通过后直接上线，不必逐次确认）

1. 运行 `npm run check`，必须全部通过。
2. 定发布编号：沿用 `R<数字>`。同一个 Unity 包上的网页层改动加字母后缀（R17A、R17B…）；新的 Unity 构建用下一个数字。`version`（目前 0.3.8）只在用户要求时改。
3. 写记录：
   - 把当前 `release.json` 移到 `releases/<当前编号>.json`，再写新的 `release.json`（字段沿用现有格式，`previousWebCommit`、`previousBackupBranch` 指向上一版）。
   - `RELEASES.md` 顶部加一条：改了什么、怎么验证的。
   - 加载页右下角的版本号（`#loading-version`）改成 `v<version> · <编号>`。`npm run check` 会核对它和 `release.json`、`productVersion`、`package.json` 一致。
4. 提交到工作分支，快进合并到 `main`：`git push origin <工作分支>:main`。
5. 备份：给新上线的提交建分支 `backup/<编号>`：`git push origin <commit>:refs/heads/backup/<编号>`。每个上线版本都有自己的 backup 分支；本环境不能推送 Git 标签，所以用分支。
6. 确认 GitHub Actions 的「Quality checks」和「pages build and deployment」都成功，再告诉用户上线的编号。

## 回退

不改写历史：把目标版本的文件原样恢复，作为一次新提交上线。例如回到 R17：

```
git fetch origin backup/R17
git rm -r -q . && git checkout origin/backup/R17 -- . && git commit -m "Roll back to R17"
git push origin HEAD:main
```

回退本身也是一次上线：照常写记录（编号继续往后排）并建备份分支。
