# CHO-SIREN 两首选定歌曲 — R69

v0.3.78 / R69 已上线并完成验收。正式提交10372efb1e97db15714d81c616301e9352b5b4bb；main与backup/v0.3.78分别推送并核对一致。制作于2026-10-16，正式发布验收于2026-10-17（上海）。

公开地址：https://bojuejun.github.io/cho-siren-preview/?v=0.3.78

用户明确要求去掉Neon Moonwalk。HomeMusicPlaylist现在Count=2，顺序光的规则（爵士说唱-1）→Chase-the-Glow-A→光的规则，NativeResourceTrackIndex从1调到0，保持原生bgm.wav与已选原录音身份一致。GameAudio无三首硬编码，按稳定曲目ID维护书签，因此不需要改传输/CG路由。切歌按钮动态显示1/2、2/2。

只改HomeMusicPlaylist、现有专用测试、构建助手的Count循环与版本号、ProjectSettings版本。原两份MP3逐字节与R68相同，音量0.18、战斗伴奏0.055、CG音乐优先、独立静音/音效与续播不改。移除的是当前播放选择；上一R68兼容引擎与它可能引用的历史媒体仍保留，以免旧缓存报错。

18/18真实音频PlayMode回归通过，覆盖两首自然结束轮转、书签、CG切歌保持安静、快速切歌/跨页取消、静音不下载和按钮绑定。WebGL构建成功，356562270bytes，80.102秒；106/106网页检查通过。源码只同步4条scope并保留备份，1659条其他既有文件哈希未变。

本地真实WebGL先后播放光的规则172.3335秒、Chase158.7335秒、再回光的规则；始终只有1个长音源、48000Hz、gain0.18、输出非零，返回第一首恢复73.04秒书签。QA请求日志中没有Neon请求。公开Chrome390×844载入100%、显示v0.3.78并进入大厅，按钮实测1/2→2/2→1/2，0运行错误；原静音偏好保留，未领取签到或开战消耗体力。公开20文件完整SHA一致，含当前引擎4件、原两首、战斗伴奏、人物与骰子；R68上一引擎4件HEAD可用。本轮未生成/付费新音乐，没有使用Devin。实体手机未测试。


## 同一正式提交CI与证据

- Quality checks / 39039770663 / success / https://github.com/BOJUEJUN/cho-siren-preview/actions/runs/39039770663 / 状态更新2026-10-17T14:13:06Z
- Quality checks / 39039711831 / success / https://github.com/BOJUEJUN/cho-siren-preview/actions/runs/39039711831 / 状态更新2026-10-17T14:12:18Z
- pages build and deployment / 39039710906 / success / https://github.com/BOJUEJUN/cho-siren-preview/actions/runs/39039710906 / 状态更新2026-10-17T14:13:05Z

全部head_sha与正式提交一致。updated_at为CI状态更新时间，不冒充job精确完成时间。证据目录TWO-SELECTED-SONGS-EVIDENCE-2026-10-16/保存测试XML、ci-final、public-byte-verification、public-runtime-r69、本地实播JSON及截图、源码保护备份报告、原录音hash等。仅移出当前播放列表，历史音频/上一版兼容资源保留。
