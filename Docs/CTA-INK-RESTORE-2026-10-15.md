# R66 / v0.3.71 开始演出墨迹范围恢复

用户要求：只有开始演出的悬停边缘恢复缩小前大小，颜色保持绿色；其他三个入口沿用R65。

实现仅恢复`PsdHomeAlbumInkMotion.InitializeBehindCallToAction`的原四角中心1.3倍外扩。绿色(.3,.86,.14)、48帧自然RGBA、轻微impact、任务/专辑/练习室AroundArt范围和全部触区保持。源组件除CTA初始化及对应注释外逐字一致。正式源按修改前SHA保护同步此组件与版本号两文件，其他脏工作保留。

已有3项实际悬停/触区PlayMode通过，网页106项检查通过（bundled Node直接执行package check同等三命令；本地无npm）。新WebGL构建成功。Chrome逐一悬停确认演出恢复较大绿色范围，其他三个保持R65范围，移出退出。没有新增素材、音乐生成或付费调用，没有重跑并冒称R65音乐/编队测试。实体手机/iOS包未验收。

R65完整四件保留为上一版，图片/音频/音频桥/加载脚本保持R65。证据见`CTA-INK-EVIDENCE-2026-10-15/`；完整构建日志在当前任务`work/cta-ink-restore-20261015/build-webgl.log`。正式提交、Actions和公开页结果见下节。下一版v0.3.72 / R67。

## 已部署并完成公开验收

main与backup/v0.3.71已单独推送8712c84546a0d36bb0c53d32586665b3dcd2a2f6，正式Pages仓库已快进且保留原分支名。正式release.json显示0.3.71。main Quality 38723412279、backup Quality 38723433938及Pages 38723410059均success，5个job成功。正式Chrome完成显式ready、error=null，鼠标移入演出后已目视确认原较大范围的绿色动态墨迹。原首页、资源、数值和音乐false偏好保持。当前及完整R65八资源均已做远端完整SHA一致核验（两大data均并发Range/206重建），共43个引擎/网页/音乐文件全部匹配8712c84。QA页已关闭，本轮本地8797服务器已停止，正式页保留。
