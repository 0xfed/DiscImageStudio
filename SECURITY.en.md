# Security Policy

[简体中文](SECURITY.md) | [繁體中文](SECURITY.zh-TW.md) | [English](SECURITY.en.md) | [日本語](SECURITY.ja.md) | [Türkçe](SECURITY.tr.md)

Do not disclose exploitable security issues in public issues. Use the GitHub repository's private **Security → Report a vulnerability** feature and provide the affected version, reproduction steps, and scope of impact.

The desktop application and CLI do not initiate network connections, but they parse images, directories, and disc image files selected by users. Issues involving file parsing, path handling, package manifests, or untrusted input should all be treated as security issues.

## Dependency and build trust boundaries

CI selects CPython `3.14.10` through `.python-version` and uses SDK `10.0.403` and runtime `10.0.14`. `packaging/Install-DotNetSdk.py` downloads the SDK from a fixed official URL, compares its SHA512 with the reviewed value in `eng/dependencies/dotnet-sdk.json` before extraction, and installs into a fresh directory. NuGet restores require approved repository signatures, Python dependencies use the wheel hashes in `requirements-ci.lock`, and Actions pin commits; build provenance uses `actions/attest` `4.2.2`. These checks constrain build-input identity and do not prove that upstream code is benign; Python bootstrap, hosted runners, and system tools retain external trust boundaries.

Pillow `13.0.0` wheels used by Python research/CI tools still contain Brotli `1.2.0`, affected by `CVE-2026-111497`; no verified newer release wheel currently provides a direct replacement. No direct ImageFont/WOFF2 loading call was found in the current Agg/PNG path, and exploitability remains unproven; Pillow is not shipped with the .NET desktop application or CLI. Upgrading the separate Python Brotli package does not replace Pillow’s embedded native library. Remaining advisory matches in Actions’ bundled dependencies also need review against actual call paths; this upgrade does not resolve them all.
