# Android 官网下载修复 · 2026-10-28

## 原因与修复

COS 默认域名对 APK 的真实 GET 返回 `403 DownloadForbidden`，即使 HEAD 返回 200 也不能证明能下载。腾讯云要求使用自定义域名，见[官方说明](https://cloud.tencent.com/document/product/436/102489)。当前账号未开通 CDN，现有部署密钥也没有查询自定义域名/DNSPod 的权限，因此改由现有同仓库 GitHub Releases 分发 APK，COS 中的安装包和旧版本继续保留。

- 当前已公开包：[CHO-SIREN 0.9.9 APK](https://github.com/BOJUEJUN/cho-siren-preview/releases/download/v0.9.9/CHO-SIREN.apk)。
- 官网两个安卓按钮改为 `https://github.com/BOJUEJUN/cho-siren-preview/releases/latest/download/CHO-SIREN.apk`，需随官网提交部署后生效。
- 新增 `Tools/github_android.py`，复用既有 Git 凭据，先上传草稿、校验服务器 SHA256/长度，再发布并匿名 GET 校验 APK 字节。重复执行同版本会复用相同文件；不会删除旧包或覆盖不同内容。
- `Tools/release-installers.sh` 同时保留 COS 归档，但 `app-version.json` 的 Android 链接使用已校验的 GitHub 版本 URL，后续版本不再退回被封禁的默认 COS APK 链接。
- 远端现行 0.9.9 `app-version.json` 已热修，仅 Android 字段改为固定 v0.9.9 下载链接；版本、changelog 和其他平台字段经真实 GET 比对完全保留。原文件备份及热修证据在同一 QA 目录。

## 已验证

- stable latest 匿名 GET：HTTP 206，`Content-Type: application/vnd.android.package-archive`。
- `Content-Range: bytes 0-65535/369815081`；前 65536 字节与本地签名 APK 完全相同。
- GitHub 文件大小与本地均为 369815081 字节；服务器 SHA256 与本地均为 `6794742ef9b5ba7a0e5a93c01df7dafb12b0031287441c3f5bcc601d470b530b`。
- 新发布脚本执行两次均成功，第二次复用现有 release；Python 编译检查和 shell 语法检查通过。
- 证据：`Builds/qa-logs/iphone-099/android-download-proof.json`。

没有连接 Android 真机；这里验证的是匿名下载路径、APK 内容与原签名包一致，不冒充手机点击安装验收。后续配置国内下载自定义域名后，可将入口迁回腾讯云加速。
