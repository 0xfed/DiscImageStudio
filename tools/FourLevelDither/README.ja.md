# 白黒ディザリングツール

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

元画像を **0 と 255** だけの白黒 2 階調 PNG に変換する、独立した Windows 画像処理アプリケーションです。DVD と CD はどちらもこの 2 階調を使用し、白黒ピクセルの空間的な混合で明暗を保ちます。既存の起動方法との互換性のため、実行ファイル名は `FourLevelDither.exe` のままです。

## ウィンドウ操作

1. `FourLevelDither.exe` を起動し、「画像を開く」を押すか、画像をウィンドウにドラッグします。
2. 「DVD 2 階調」または「CD 2 階調」を選び、対応する符号語の説明を確認します。両方とも同じ白黒パレットを出力します。「100% ピクセル表示」でディザリングの細部を確認できます。
3. 「sRGB ガンマ補正」は既定でオフで、従来の出力を保ちます。オンにすると自動で再処理され、現在の設定が表示されます。
4. 「白黒 PNG を保存」で処理後の画像を別名保存します。補正がオフの場合、既定のファイル名は `-黑白抖动.png`、オンの場合は `-黑白抖动-sRGB.png` で終わり、比較しやすくなります。現在の元画像パスへの上書きは拒否します。

ディスク種別やガンマを切り替えると、キャッシュした元画像から再計算し、前回のディザリング結果を再処理しません。以前の 3 階調や 4 階調から 2 階調に移行する場合、できるだけ完全な明暗情報を保つため、ディザリング前の元画像を開いて再処理してください。

PNG、JPEG、BMP、TIFF、GIF など、システムの画像デコーダーが対応する形式を使用できます。可逆な Gray8 PNG として出力し、元のピクセル寸法と有効な DPI を保持します。DPI が無効な場合は 96 を使います。プレビューのズームは保存するピクセル寸法を変更しません。

## 処理規則とディスクマッピング

既定では、符号化 RGB 空間で Rec.709 の重みを使って輝度に変換し、透明部分を先に白背景と合成します。その後、**蛇行走査の Floyd–Steinberg 誤差拡散**を行います。隣接する行で走査方向を交互に変え、伝播誤差を加えた輝度に最も近い黒か白を選び、輝度差を後続ピクセルに分配します。

| メインアプリの 2 階調入力範囲 | PNG / プレビューのグレー値 | DVD スクランブル領域の Q byte | CD byte |
| --- | ---: | --- | --- |
| 0–127 | 0 | `0xA5` | `0x0D` |
| 128–255 | 255 | `0x92` | `0xAA` |

既定のガンマ補正オフでは、元のグレー値 128 の広い領域は、約 `128 / 255 = 50.2%` が白ピクセル、残りが黒になります。ディザリング中は伝播誤差が加算されます。ガンマ補正のオン／オフにかかわらず、出力ファイルは 0 と 255 だけを含みます。

### sRGB ガンマ補正

オンにすると、各 RGB チャンネルを標準 sRGB 伝達関数で線形光にデコードし、Rec.709 の重みで輝度を求め、線形空間で白背景を合成して誤差を拡散します。白黒ピクセルの配分比が変わりますが、ディスクの符号語や出力階調は変わりません。

例えば、元のグレー値 128 は約 21.6% の線形輝度にデコードされ、約 21.6% の白ピクセルになります。同じ画像の中間調は、通常オフの場合より暗くなります。両方を保存して実ディスクの結果を比較できます。オフにすれば従来のアルゴリズムに戻り、既定の動作は変わりません。

メインアプリのトーン設定はデザイン画面の右列に集約され、生成、ストリーミング書き込み、デザイン投影で共有されます。この独立ツールで先に PNG を出力する必要はありません。新規プロジェクトの既定値はディザリング 75%、2 階調、明るさ 0、sRGB リニア化オフです。CD は 2/4 階調、DVD/BD は 2 階調を使用します。生成ジオメトリはプロジェクト設定で編集します。

現在のメインアプリはレイヤーからデザインを合成し、以前の CD/DVD リング複製オプションは提供しません。独立ツールは 1 枚の画像だけを処理し、プロジェクトのレイヤー配置は管理しません。

処理の中核 `DiscImageStudio.Imaging.GrayscaleDither` は、引き続き任意の順序付きグレースケールパレットに対応します。旧 `FourLevelDither` API は既存の符号語プローブなどのためだけに互換動作を残し、独立ウィンドウと CLI はどちらも白黒 2 階調を使います。

## コマンドライン

ウィンドウとコマンドラインは簡体字中国語、繁体字中国語、英語、日本語、トルコ語に対応し、起動時に言語を決定します。`--language zh-CN|zh-TW|en|ja|tr|system` で明示的に選択できます。未指定の場合は環境変数 `DISC_IMAGE_STUDIO_LANGUAGE` を使い、なければシステム UI 言語に従います。言語選択はモード値、ファイル名、画素出力を変更しません。

```powershell
# ウィンドウなしで変換。既定は DVD の 2 階調。出力は未作成の .png が必要。
.\FourLevelDither.exe --input original.jpg --output result.png

# DVD または CD を明示指定。どちらも 0/255 の白黒 2 階調を出力。
.\FourLevelDither.exe --input original.jpg --output dvd-result.png --mode dvd
.\FourLevelDither.exe --input original.jpg --output cd-result.png --mode cd

# sRGB ガンマ補正のオフとオンを比較。--gamma の省略は none と同じ。
.\FourLevelDither.exe --input original.jpg --output result-none.png --gamma none
.\FourLevelDither.exe --input original.jpg --output result-srgb.png --gamma srgb

# 画像を読み込んでウィンドウを開く。
.\FourLevelDither.exe original.jpg

# ヘルプを表示（--help も使用可能）。
.\FourLevelDither.exe -h

# 日本語でウィンドウを開く
.\FourLevelDither.exe --language ja original.jpg

# トルコ語でウィンドウなしの変換を実行
.\FourLevelDither.exe --language tr --input original.jpg --output result-tr.png
```

## ソースから実行・発行

リポジトリのルートで .NET 10 SDK を使用します。

```powershell
dotnet run --project tools/FourLevelDither --configuration Release

dotnet publish tools/FourLevelDither/FourLevelDither.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --output artifacts/FourLevelDither/win-x64
```

自己完結型の `FourLevelDither.exe` には .NET 10 ランタイムが含まれます。非自己完結型のビルドを直接使用する場合は、.NET 10 Windows Desktop Runtime をインストールする必要があります。
