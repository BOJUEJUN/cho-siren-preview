# CHO-SIREN R68 — 已上线并验收

2026-10-16，记录时间（上海）2026-10-16T19:01:09+08:00。

- 版本：v0.3.77 / PSD-20261016-COMPACT-BATTLE-R68。
- 正式提交：303927d70df1bf809185b23c5f8f74b8f4dfebb6。
- 试玩：https://bojuejun.github.io/cho-siren-preview/?v=0.3.77 。
- main 与 backup/v0.3.77 已分别推送，远程引用均指向上述提交；没有强制推送。
- 用户本轮明确授权“上线”，此前自动审批的发布授权阻塞已解除。

## 最终变更

加载页公告、修复、声音、16+统一右侧一列，原功能和样式保留。公开390×844视口实测4按钮x相同，最小宽58.5px；年龄按钮位于第四项。

玩家战斗卡保留完整半身头部；HP/真实攻击/蓝色能量条集中在立绘下方，HP和攻击字号18，卡片高度1580→1370source。攻击读数置于能量轨道之上，五位HP容纳、技能图标、队长与守护44CSSpx点击区保留。

普通攻击/受击/重复倍率浮字减少；小技能不再叠出重复角色小立绘，技能名、语义图标和真实伤害/治疗/护盾保留。弱骰型只保留局部反馈，强骰型改为手牌区域0.85秒短提示，不遮盖战场。角色普攻、受击、待机、就绪与大招动画仍在。

关卡显示建议等级与队伍平均等级，高培养时显示适合速刷，不再固定称“约1分钟”。没有改基础战斗数值、伤害公式、收益或玩家培养存档。原首页两张用户角色和新版六面骰子保留。

## 音乐边界

原Home顺序仍为Neon Moonwalk、爵士说唱-1/光的规则、Chase-the-Glow-A，支持下一首；原两首用户选定录音在源工程/发布树/R67三处逐字节一致，公开两份MP3完整SHA也一致。

- 光的规则：1c7f72fed9af8b53d93b4f572006a9c780545c52cd5e32e96b8d64468124a267。
- Chase：5d360e8e7acbc7c6f0f34a2fcf214750f9ce0d89f9d7d13b04468b0d74b2ffff。

新增4首候选未接入游戏，用户不接受中文方向后全部撤出待发布页面/音频/元数据。此次使用与已验收树完全相同的干净提交发布，parent为正式R67 11b85072fa56ec4197193a4f5144f67b47655abc；包含误加试听的本地提交af1b9318及其父提交完整保留在原工作分支，没有推送到远程历史。4首原始文件/试听副本/歌词与ZIP仅本地归档。

用户要求后续新增演唱按日文方向；不能据此重生成或替换已经选定的录音。原Chase制作说明记载韩语加少量英文，不能断言两首均日文。战斗专用伴奏gain0.055、普通菜单静音、CG自带音乐优先、独立音乐/音效与续播逻辑保持。线上验收保持玩家原静音设置。

## 测试证据

- 58个唯一PlayMode用例按每例最新真实结果聚合，58/58通过。原完整包57/58，后续能量10/10解决旧隐藏标签断言；最新反馈19/19覆盖最终小立绘清理。不是单个原始58项包全绿。
- 36次真实战斗模型模拟（3场景×2大招模式×6seed），1个审计测试通过；全部使用内存存档，不改用户资料。
- 最终Unity WebGL构建成功，356560493bytes，70.64秒；构建标记build-webgl-final。
- 发布前网页106/106通过、Service Worker语法与完整当前/上一包验证通过。
- 源码保护同步15路径，相对首次备份15变化；末次仅同步Main与对应测试两项。原备份不覆盖，原始范围外1644文件hash保持不变。源模板仅同步布局，保留构建占位符。

## 正式发布与公开验收

以下全部为同一exact SHA 303927d70df1bf809185b23c5f8f74b8f4dfebb6：
- Quality checks / 38890486061 / success / https://github.com/BOJUEJUN/cho-siren-preview/actions/runs/38890486061 / 2026-10-16T10:56:27Z
- Quality checks / 38890360736 / success / https://github.com/BOJUEJUN/cho-siren-preview/actions/runs/38890360736 / 2026-10-16T10:55:28Z
- pages build and deployment / 38890357588 / success / https://github.com/BOJUEJUN/cho-siren-preview/actions/runs/38890357588 / 2026-10-16T10:56:59Z

公开21个文件全字节SHA一致，含99.51MB data（Range/206分块重组完整校验）、framework、wasm、loader、index、release、角色素材、新骰面和原4份音乐资源。完整R67上一引擎的4文件仍在线，HEAD仅验证其可用，不称已重下全字节；本地上一包校验通过。

真实Chrome 390×844加载到100%、显示v0.3.77、四入口竖列；点击进入大厅成功，保留原两张角色与3曲切换入口。关闭签到未领取奖励，没有消耗公开存档体力。运行错误0，仅既有Unity文件系统同步弃用警告。最终包本地实际stage-1-2战斗验收了完整头部、下方紧凑数值、无重复小立绘，且线上引擎四文件全字节相同。

## 节奏审计边界

截图stage-1-9开局敌HP28819、两波共43669。截图阵容等级68/57/64/59，平均62，对建议9级关卡实测9.0–13.125秒结束；起始能量0、20秒首大招、倍率单次计算未发现残留或重复乘算。部分装备防御为明确夹具假设，并非获得完整实际用户装备。

一级新手stage-1-1为70–97秒、12/12胜；建议9级无主动操作队伍每模式3/6胜，另有120秒超时。当前最高建议等级20，不能据本轮审计宣称完整关卡难度已平衡，也没有通过削培养或膨胀敌HP强拖时长。

## 证据与限制

证据目录BATTLE-CLEANUP-EVIDENCE-2026-10-16/：ci-final.json、public-byte-verification.json（21全SHA+4可用）、public-runtime-r68.json、public-column-r68.json、selected-music-preserved.json、publish-state.json、public-loading-r68.jpg、public-lobby-r68.jpg、local-battle-final-build.jpg、regression-summary.json、battle-pace-audit.json等。

实体手机/iOS安装包本轮未测试；Chrome手机尺寸验证不能替代实体设备。之前准备/误生成/撤出过程仅为历史，存于work/battle-cleanup-20261016/BATTLE-CLEANUP-predeployment-history.md。没有使用Devin。
