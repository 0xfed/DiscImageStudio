# Security Policy

[简体中文](SECURITY.md) | [繁體中文](SECURITY.zh-TW.md) | [English](SECURITY.en.md) | [日本語](SECURITY.ja.md) | [Türkçe](SECURITY.tr.md)

请不要在公开 Issue 中披露可被利用的安全问题。请使用 GitHub 仓库的 **Security → Report a vulnerability** 私密报告功能，并提供受影响版本、复现步骤和影响范围。

桌面应用与 CLI 不会主动连接网络，但会解析用户选择的图片、目录和镜像文件。涉及文件解析、路径处理、打包清单或不受信任输入的问题都应按安全问题处理。

## 依赖与构建信任边界

CI 通过 `.python-version` 固定 CPython `3.14.10`，并使用 SDK `10.0.403` 和运行时 `10.0.14`。`packaging/Install-DotNetSdk.py` 从固定的官方地址下载 SDK，解压前与 `eng/dependencies/dotnet-sdk.json` 中经审查的 SHA512 比对，并安装到全新目录。NuGet 还原要求批准的仓库签名，Python 依赖使用 `requirements-ci.lock` 中的 wheel 哈希，Actions 固定提交；构建来源证明使用 `actions/attest` `4.2.2`。这些检查约束构建输入的身份，不能证明上游代码没有恶意行为；Python 引导程序、托管运行器和系统工具仍有外部信任边界。

Python 研究/CI 工具所用 Pillow `13.0.0` 的 wheel 仍包含受 `CVE-2026-111497` 影响的 Brotli `1.2.0`；目前没有已验证的新版发布 wheel 可直接替换。现有 Agg/PNG 路径未发现直接加载 ImageFont/WOFF2 的调用，实际可利用性尚未证实；Pillow 不随 .NET 桌面应用或 CLI 发布。单独升级 Python Brotli 包不会替换 Pillow 内嵌的原生库。其余 Actions 内嵌依赖的公告匹配也仍需按实际调用路径跟踪，不应将本次升级视为全部修复。
