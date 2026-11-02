# 2026-11-02 手机验收版 0.9.26

已覆盖安装到伯爵君 iPhone，并通过 devicectl 查询确认 0.9.26。同一 bundle ID 保留存档；按用户要求未启动。

本轮修改：
- 队伍页笔跟随名称框右侧，闪电跟随战力数字左侧；4–7 位战力间距验证通过。
- 关卡队伍栏显示四位实际成员的头像与名字，交换按钮旋转、青色短脉冲，沿用真实队伍切换弹窗。
- 四人战斗胸像按人脸锚点统一放大；修正 ApplyCell 刷新覆盖裁框的问题，血蓝条不动。
- 得分骰子使用圆角方形贴边流动碎光，中心透明，移除圆环与长尖峰。仅 Hand.Participating 骰子点亮，取消同组依次亮的延迟。
- 编队拖动真实跟随鼠标/手指，轻微放大倾斜，松手后换位或回位，队长身份保留。

验证：PlayMode focused 5/5；立绘刷新补修后截图复测 1/1。
证据：Builds/qa-logs/review-polish-1102；四人立绘最终截图 Builds/qa-logs/zj-1031/hero-card/hero-card-rest.png。
Unity iOS 导出成功，Xcode BUILD SUCCEEDED，签名检查通过；install-bojuejun.json 与 installed-app.json 已记录成功安装及版本。
复用 iOS-device-0.9.24 / iOS-DerivedData-device-0.9.24 两个缓存目录，其内容现为 0.9.26，不能从缓存目录名推断版本。

用户本次要求先到手机验收。公开 Web 仍为 0.9.25，其他公开客户端仍为原版本，本次未全量发布。五条悟仍暂缓。
