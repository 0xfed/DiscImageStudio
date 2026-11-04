# 安全性政策

[简体中文](SECURITY.md) | [繁體中文](SECURITY.zh-TW.md) | [English](SECURITY.en.md) | [日本語](SECURITY.ja.md) | [Türkçe](SECURITY.tr.md)

請不要在公開 Issue 中揭露可被利用的安全性問題。請使用 GitHub 儲存庫的 **Security → Report a vulnerability** 私密回報功能，並提供受影響版本、重現步驟與影響範圍。

桌面應用程式與 CLI 不會主動連線至網路，但會剖析使用者選取的圖片、目錄與映像檔。涉及檔案剖析、路徑處理、套件資訊清單或不受信任輸入的問題，都應視為安全性問題處理。

## 相依套件與建置信任邊界

CI 透過 `.python-version` 固定 CPython `3.14.10`，並使用 SDK `10.0.403` 與執行階段 `10.0.14`。`packaging/Install-DotNetSdk.py` 從固定的官方位址下載 SDK，解壓縮前與 `eng/dependencies/dotnet-sdk.json` 中經審查的 SHA512 比對，並安裝至全新目錄。NuGet 還原要求核准的儲存庫簽章，Python 相依套件使用 `requirements-ci.lock` 中的 wheel 雜湊，Actions 固定提交；建置來源證明使用 `actions/attest` `4.2.2`。這些檢查限制建置輸入的身分，不能證明上游程式碼沒有惡意行為；Python 引導程式、託管執行器與系統工具仍有外部信任邊界。

Python 研究/CI 工具使用的 Pillow `13.0.0` wheel 仍包含受 `CVE-2026-111497` 影響的 Brotli `1.2.0`；目前沒有已驗證的新版發行 wheel 可直接替換。現有 Agg/PNG 路徑未發現直接載入 ImageFont/WOFF2 的呼叫，實際可利用性尚未證實；Pillow 不隨 .NET 桌面應用程式或 CLI 發行。單獨升級 Python Brotli 套件不會替換 Pillow 內嵌的原生程式庫。其餘 Actions 內嵌相依套件的公告比對也仍需依實際呼叫路徑追蹤，不應將本次升級視為全部修復。
