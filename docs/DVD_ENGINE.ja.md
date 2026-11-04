# DVD NRZI 画像制約ソルバー

[简体中文](DVD_ENGINE.md) | [繁體中文](DVD_ENGINE.zh-TW.md) | [English](DVD_ENGINE.en.md) | [日本語](DVD_ENGINE.ja.md) | [Türkçe](DVD_ENGINE.tr.md)

このプロジェクトは連続する 16 個の DVD データセクターを 1 つの ECC Block として扱い、2 次元画像または指定された NRZI pit/land 制約に基づいて読み取り可能なユーザーデータを探索します。

固定の規約：

- 単層ディスクの Data Zone、Sector Information は `0x00`；
- DVD-R Data Frame の RSV は 6 個の `0x00`；
- ECC Block の先頭 LBA だけを入力し、後続セクターは自動的に `LBA + 1 ... LBA + 15` を使う；
- `PSN = PSN offset + LBA`、既定の offset は `0x30000`；
- DSV の絶対値が等しい場合は Stream 1 を選ぶ；
- `pit = 0`（黒）、`land = 1`（白）；
- 出力は毎回 32768 バイト、つまり連続する 16 個の 2048-byte payload。

符号化チェーンは ID、IED、全ゼロ RSV、EDC、データスクランブル、RS-PC PO/PI、PO 行インターリーブ、EFMPlus 主/代替テーブル、SYNC/DSV 制御、NRZI 変換を含みます。

独立した[階調実験ツール](../tools/DvdGrayProbe/README.ja.md)は `FastDispersionImageWriter.WriteScrambledCodewordPatternStream` を使い、同じ payload 符号語中心座標系でスクランブル後の Q を直接指定し、`P = Q XOR mask` を書き出します。固定の階調区分を通らないため、通常の画像生成パレットを変えずに `64/E4` などの候補符号語や離散ディザアトラスを試せます。高速生成はディスク全体の ECC/EFM を再生せず、光学的な明るさも予測しません。回帰テストは混合符号語の小さなサンプルを完全な ECC/EFM で再生し、実際の payload、幾何、並列実行の一致、キャンセルも別途検証します。

## ビルドとセルフテスト

以下は Windows の独立した入口を使うコマンドで、.NET 10 SDK が必要です。その非自己完結型ビルドを直接実行するには .NET 10 Windows デスクトップランタイムが必要です。Linux/macOS では共通 CLI から DVD コマンドを実行できます。[README.md](../README.ja.md) を参照してください：

```powershell
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DvdImageSolver --configuration Release -- selftest
```

セルフテストの範囲：

- ECMA-267 変調テーブルの抜き取り検査；
- ID、IED、RSV、EDC；
- スクランブル XOR の往復；
- 208 行の PI と 172 列の PO の剰余がゼロ；
- 619008 個の物理 NRZI レベルの個数、決定性、RLL(2,10) データラン；
- pit/land の両初期レベルでの 16-bit 符号語の事前計算階調、終了レベル、DSV 増分；
- ランダムな SYNC Frame の符号語再生階調、SYNC 選択、最終 State/DSV/レベルがビット単位のエンコーダーと一致；
- 代替 SYNC の再生が不要な場合、2 ストリームの EFMPlus 接頭状態がビット単位のエンコーダーと完全一致；
- state-control の最終境界が完全な DVD 順方向符号化と一致し、求解中に 619008-bit レベル配列を実体化しない；
- dispersion の `0x92/0xA5` スクランブル領域マッピング、逆スクランブルした ISO payload、最終境界が完全符号化と一致；
- 最小 ISO9660 記述子、Path Table、ルートディレクトリ、payload LBA、テンプレート置換結果；
- 半径キャリブレーション PNG プレビューと複数 ECC Block にまたがる連続 RAW イメージ出力；
- 内周 ISO9660/Joliet ファイル、Unicode サブディレクトリ、外周描画データの分離保護。

## ターゲットファイル

ターゲットファイルは物理 NRZI レベルを文字で表します：

| 記号 | 意味 |
| --- | --- |
| `0` または `P` | pit、黒 |
| `1` または `L` | land、白 |
| `?` | 制約なし |

空白とアンダースコアは無視します。ファイルには次のいずれかが必要です：

- 38688 記号：1 つの物理セクターを制約し、`--target-sector 0..15` で ECC Block 内の位置を指定；
- 619008 記号：完全な ECC Block を制約。

SYNC、RLL、EDC、ECC は実現可能なパターンを制限するため、任意のビット単位画像には通常、誤差ゼロの解がありません。`full` は不一致の NRZI ビットを目的とします。画像の `state-control` はターゲットを黒/白に 2 値化し、まず pit/land の多数方向の誤り、次に実際の land 数と純黒 0/純白 16 との階調距離を評価します。探索目的には制御可能な payload 符号語だけを使い、最終可視性は全チャネル符号語を数えます。

`dispersion` は pit/land の絶対多数を制御せず、EFMPlus の遷移密度で白黒 2 階調テクスチャを作ります。スクランブル後バイト `Q=0xA5/0x92` は全状態の表現でそれぞれ必ず 5/2 回遷移します。画像輝度 0–127 は A5、128–255 は 92 に対応し、プレビュー輝度は 0/255 です。新規生成では 66 や B9 を独立した階調に使いません。NRZI 全体を反転しても遷移位置は変わりません。

ディザ処理済みで完全不透明な階調画像が `0/255` だけを含む場合（全黒/全白も可）、生成とキャリブレーションプレビューは最近傍ピクセルを取り、元の階調を保ちます。連続階調、カラー、透明度のある画像は引き続きバイリニアサンプリングを使います。メインアプリはデザインページで拡縮、回転、レイヤー合成を行い、生成、ストリーミング書き込み、プレビューは合成画像と `ToneProfile` を共有し、環状コピーや前処理を別途行いません。`full` と `state-control` の 2 値ターゲットは従来のサンプリング規則を保ちます。

## 画像マッピング

PNG、JPEG、BMP、TIFF、GIF を直接入力できます：

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2295104 `
  --iso-output solved-disc.iso `
  --output payload.bin `
  --lba 0 `
  --psn-offset 0x30000 `
  --inner-radius-mm 24 `
  --outer-radius-mm 58 `
  --channel-bit-nm 133.3 `
  --start-angle-deg 0 `
  --spiral-direction ccw `
  --image-threshold 128 `
  --alpha-threshold 1 `
  --iterations 10000 `
  --constraint-output mapped-target.txt `
  --channel-output channel.bin
```

`--total-sectors` はディスクが報告する 2048-byte データブロック総数で、別名 `--total-blocks` も使えます。セクターあたり 38688 チャネルビット、`--channel-bit-nm` と合わせて総トラック長を決め、内外半径とピッチ曲線からピッチ尺度を逆算します。

### 半径に応じて変化する 3 次トラックピッチ

DVD 生成、ストリーム出力、画像プレビュー、ISO 読み戻し、補助キャリブレーションは `DvdTrackGeometry` を共有します。正規化半径 `x=(r-ri)/(ro-ri)` に対し、局所ピッチは次のとおりです：

```text
p(x) = a0 + a1*x + a2*x² + a3*x³                 [mm]
     = p0 * (1 + c1*x + c2*x² + c3*x³)
dr/dθ = p(x)/(2π)
```

完全な 3 次多項式は 4 係数を持ちます。`--pitch-linear`、`--pitch-quadratic`、`--pitch-cubic` は無次元の相対係数 `c1,c2,c3` です。定数項 `a0=p0` は固定総トラック長から自動で解き、残りは `ai=p0*ci` です。4 項は独立ではなく、容量と内外半径の長さ制約を満たします。UI は全 4 項を μm で表示し、変数は `x` であって mm 単位の絶対半径ではありません。

トラック長は `L=∫sqrt(1+(2πr/p(r))²)dr`、角度は `θ(r)=∫2π/p(r)dr` を使います。可変ピッチでは一定ピッチの角度式 `(r-ri)/b` を使えません。多項式の内部極値を含む生成環全域でピッチが厳密に正であることを検証します。3 項がすべてゼロなら従来の一定ピッチモデルとなり、旧パラメーターやプリセットで省略したフィールドはゼロです。

例えば元の `solve` に `--pitch-linear 0.02 --pitch-quadratic -0.03 --pitch-cubic 0.015` を追加します。これは書式例であり、特定のディスクの測定値ではありません。`calibrate` は同じオプションで生成曲線を、`--actual-pitch-linear`、`--actual-pitch-quadratic`、`--actual-pitch-cubic` で実曲線を指定し、`--generated-pitch-*` も受け付けます。`simulate` の `--pitch-*` は実ディスクの曲線です。

イメージ/プレビュー JSON は相対係数と基準ピッチを記録し、キャリブレーションセッションは相対係数、内外半径、セクター数、ビット長を保存して全 4 項を再構築できます。写真全体の回転角は独立したフィッティング量です。

座標とサンプリングの規約：

- 画像中心はディスク中心、短辺は外径に対応し、長辺の余剰領域は保持；
- 内半径で 0° から開始し、0° は画像の右方向。既定は数学的な反時計回りで、`--spiral-direction cw` で時計回りに変更；
- 黒は pit、白は land、`--image-threshold` 未満を黒とする；
- alpha が `--alpha-threshold` 未満の透明領域には制約を課さない；
- `--lba` はトラック先頭データブロックからゼロ起算で、そこから連続する 16 セクターだけをマッピング；
- 任意の `--constraint-output` は実際の 619008-symbol 線形制約を検査用に書き出す。

## 求解

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --target target.txt `
  --output payload.bin `
  --lba 0 `
  --psn-offset 0x30000 `
  --initial-state 1 `
  --initial-dsv 0 `
  --initial-level land `
  --target-sector 0 `
  --iterations 10000 `
  --mutation-bytes 4 `
  --seed 1 `
  --channel-output channel.bin
```

主なパラメーター：

- `--lba`：ECC Block の先頭 LBA。16 個の LBA ではない；
- `--psn-offset`：10 進または `0x` 付き 16 進、既定 `0x30000`；
- `--initial-state`：EFMPlus 状態 1～4；
- `--initial-dsv`：現在の ECC Block に入る前の累積 DSV；
- `--initial-level`：現在のブロック直前の物理状態 `pit` または `land`；
- `--seed-payload`：任意の 32768-byte 初期解。省略すると決定的な乱数初期解を生成；
- `--initial zero`：全ゼロ初期 payload を使う；
- `--channel-output`：最終 NRZI レベルも出力し、8 レベルごとに MSB-first でパック。

`--target` と `--image` はどちらか一方だけを指定します。画像モードには `--total-sectors` または `--total-blocks` も必要です。

### State-control 符号語階調求解モード

payload 位置を制限しない従来のアルゴリズムは `--algorithm full` として残し、`exact` も受け付けます。旧空間制御バイトプール方式は削除済みです。新方式は次で有効にします：

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2297888 `
  --output payload.bin `
  --algorithm state-control `
  --bridge-bytes 2 `
  --beam-width 8 `
  --retry-beam-width 32 `
  --retry-frame-fraction 0.25 `
  --batch-frames 8 `
  --batch-candidates 4 `
  --search-lanes 1 `
  --iterations 3
```

state-control は 91-byte Sync Frame ごとに求解します。EDC、PI、PO、固定フィールド、SYNC は引き続き正確に生成し、State、DSV、pit/land 境界を伝播します。自由バイト探索目的には含めず、最終全ディスク可視性には含めます。`EFMPlus state × initialLevel × currentDsv`（EFMPlus 状態、入力レベル、現在の累積 DSV）ごとに最適な黒/白バイト対を計算します。ターゲット位置では各アクティブ Stream の現在の厳密な状態に対応する黒/白だけを試し、各接頭辞に最良の `--beam-width` 経路だけを残します。画像ターゲットを含む自由区間では先頭/末尾の各 `--bridge-bytes` payload バイトも制御位置としますが、256 値を列挙せず現在の DSV バイト対から選びます。

起動時に全 65536 個の 16-bit 符号語について、初期 pit/land の両方で次を前計算します：

- 16 NRZI 位置中の land 数、つまり 0～16 の 17 階調；
- 符号語終了後の pit/land；
- 厳密な DSV 増分；
- RLL 境界検査用の先頭/末尾ゼロ数。

バイト対は全 EFMPlus 表現で階調誤差と書き込み後 `|DSV|` を評価します。合成コストは `worstGrayError * 16 + worstOutputAbsDsv`、つまり最悪階調誤差の 16 倍と最悪出力 DSV 絶対値の和です。1 階調は 15 DSV 単位より重い一方、現在 DSV の向き/大きさは近い階調のバイト選択を変えられます。よく使う DSV -256..256 は前計算し、入力時または実行中の範囲外値は切り詰めず厳密に随時計算します。対象はスクランブル後 EFMPlus に入る recording-frame バイトです。ISO の元 payload は XOR で逆マッピングするため、この数種類の値に限定されません。

内側候補は 16 チャネルレベルを展開せず EFMPlus 符号語単位で評価します。各 beam 経路は Stream 1 と Stream 2 の両方を保持し、早期に一方を誤って捨てません。主/副 SYNC、同点時 Stream 1 選択、累積 DSV、最終境界は元エンコーダーの整数規則で符号語ごとに厳密再生します。各 Sync Frame の生存列は 91 バイト全体の語単位エンコーダーでも再計算し、DSV 範囲超過時の代替 SYNC に対応します。最終結果は語単位の多数、階調、境界だけを確認し、`--channel-output` を明示した場合だけビット列を展開します。

探索目的は厳密な辞書順です。まず自由 payload の `controlled-majority-error`、同数なら `controlled-gray-error`、さらに同じなら境界/DSV を比較します。コンソール/JSON の主 `majority-error`/`score` は最終の全チャネル符号語の可視誤差です。Sync Frame は 2 SYNC + 91 データ符号語を含み、不透明画像の ECC Block の分母は `416 × 93 = 38688` 固定です。主 `gray-error` も同じ 38688 語を含みます。自由バイトの探索分数は `controlledScore` と `controlledGrayScore` に別保存し、全誤差は低いほど良好です。

外側の各 pass は現在の完全 ECC Block で各 Sync Frame の controlled majority/gray 誤差を厳密計算し、最悪の `--retry-frame-fraction`（既定 0.25）の frame を選びます。まずそれらをブロック横断 warm-start 候補として試し、乱数初期解から早く離れます。局所 EFM 評価に関係なく、EDC、PO、PI 更新と全ブロック厳密再生後にしか受理しません。現在の recording frames を再利用し、payload が変わったセクター、影響する PO 列/PI 行だけを再構築します。完全 ECC 再構築との語単位一致はセルフテストで確認します。その後 `--batch-frames`（既定 8）の小バッチごとに `--batch-candidates`（既定 4）の異なる EFM 列を並列生成/検証し、全ブロック controlled 評価の厳密改善だけを受理します。失敗バッチは単一 frame まで分割し、単一 frame 失敗時には `--retry-beam-width`（既定 32）で再試行できます。分割では最初に成功した枝だけを進み、木全体を総当たりしません。受理ゼロの pass は早期終了します。`--retry-frame-fraction 0` は毎 pass 全誤り frame を処理し、`--retry-beam-width 0` は単一 frame の広い beam 再試行を無効にします。

`--beam-width`、`--retry-beam-width`、`--batch-candidates` を増やすと ECC フィードバック後も有効な候補を見つけやすい一方、検証数にほぼ比例して時間が増えます。`--batch-frames` が大きいと局所改善は強くても PO/PI フィードバック後の全体検証で却下されやすく、小さいと安定する一方で完全再構築が増えます。`--iterations`、範囲モードの `--iterations-per-block` は閉ループ pass 数で既定 3 です。別名 `--algorithm control` は有効ですが、旧 `--frame-candidates`、`--parallel-candidates`、`--parallelism`、`--word-candidates`、`--control-bytes` は拒否します。`--mutation-bytes` は `full` 専用です。

`--search-lanes 1..8` は局所最小による約 50% 誤差を減らします。lane 1 は入力初期 payload から厳密単調に受理します。他は `--seed` 由来の決定的な独立 payload で再開し、`--initial-temperature`～`--final-temperature` の焼きなまし温度により、一時的に厳密全ブロック評価の悪い候補も受理して PO/PI の障壁を越えます。各 lane は完全 ECC 厳密再生後の過去最良を別保持します。全 lane を並列実行し、controlled majority、gray、最終 `|DSV|` の順に選ぶので、探索で最終出力が lane 1 より悪くなることはありません。既定 1 は追加コストなしです。品質重視では少数セクターで `--search-lanes 4 --iterations-per-block 10` を試せます。独立再開は画像制約のない payload も変えるため、指定内容を保つなら 1 にします。

`full/exact` は可変 payload 位置を制限しない意味で、`256^32768` 空間の総当たりや大域最適保証ではありません。画像を `--image-threshold` でピクセルごとに 2 値化し、state-control は 16-bit payload 符号語内サンプルの白黒多数決を取ります。目標は純黒 0/純白 16 のみ、8:8 同数は中心サンプルで決めます。多数方向の誤り数を先に最小化し、land 数の 0/16 からの距離を階調誤差とします。16 位置それぞれのビット模様は照合しないため、チャネルビット位置の完全対応には `full` を使います。

### Dispersion 単一バイトテクスチャモード

`--algorithm dispersion`（別名 `byte-pool`）はスクランブル領域で payload を直接生成し、乱数探索しません：

| 画像輝度 | スクランブル後バイト Q | 固定遷移数 | 公称プレビュー輝度 |
| --- | --- | --- | --- |
| 0..127 | `0xA5` | 5 | 0 |
| 128..255 | `0x92` | 2 | 255 |

ISO payload は `P = Q XOR sector scrambler byte` で計算します。

各 payload 符号語は 16 チャネルビットのディスク面中心で画像を取り、上記固定区分を選びます。`--constraint-step` や `--image-threshold` の影響は受けず、`--image-threshold` は `full/state-control` の 2 値目標だけに使います。ID、IED、全ゼロ RSV、EDC、PI、PO、SYNC は規格どおり生成します。619008 NRZI ビットを展開せず、完全 recording frames を語単位で再生し、厳密な最終 EFMPlus state、DSV、pit/land を次 ECC Block に渡します。

#### 階調ディザ

2 テクスチャ自体は白黒ですが、中間調は payload バイトごとに比例混合できます：

```powershell
--tone-dither 1 --tone-levels 2 --tone-brightness 0
```

- `--tone-dither 0..1`：0 は元の `--image-threshold` 固定しきい値を保持、1 はバイトごとにしきい値をディザ処理して白黒符号語の空間比で階調を再現。
- `--tone-levels`：DVD は 2 テクスチャのみで、2 だけを受け付ける。
- `--tone-brightness -64..64`：階調全体を移動。

ディザ位相は論理 payload バイトインデックスで決まり、プレビューのサンプリングとは独立です。`simulate` は ISO を `0xA5/0x92` で分類するためディザ結果を自然に反映します。これらを省略した出力は旧版とバイト単位で同一です。

最終 EFMPlus 境界が不要で、書き込み可能イメージを速く作るには `--fast-output true` を指定します。同じ符号語中心でサンプリングし、通常 dispersion と同じ 2048-byte セクターを作りますが、事前計算した 32 KiB スクランブルマスクで `Q` から ISO バイト `P` を直接求めます。Data Frame、EDC、PI、PO、Recording Frame を構築せず、EFMPlus、DSV、pit/land も再生しません。JSON は `exactEccEfmReplay` が `false`、`finalBoundary` が `null` です。書き込みに必要な情報の省略ではなく、ドライブが 2048-byte セクターから派生フィールドを生成します。

```powershell
& ".\src\DvdImageSolver\bin\Release\net10.0-windows\DvdImageSolver.exe" solve `
  --image target.png `
  --total-sectors 2297888 `
  --iso-output "R:\full-disc-fast.iso" `
  --fill-sectors 2297888 `
  --lba 0 `
  --psn-offset 0x30000 `
  --inner-radius-mm 24 `
  --outer-radius-mm 58 `
  --channel-bit-nm 133.3 `
  --algorithm dispersion `
  --fast-output true `
  --fast-parallelism 0 `
  --initial zero
```

`--fast-parallelism 0` は論理 CPU 数に応じて独立 ECC Block payload を並列生成し、LBA 順に書きます。`1..64` で CPU 使用を制限できます。並列化はバイト選択や出力順を変えません。

高速ファイル/ストリーム出力はハードウェア対応時、4 点倍精度 SIMD サンプリングを既定で有効にします。4 つの実符号語中心を使い、SYNC/ECC をまたぐ位置を連続等間隔とは扱いません。同一セグメントの 3 次ピッチ逆算は検索を共有し、従来の 4 回の Newton/Hermite 計算をベクトル化します。セグメント越え/境界はスカラー幾何に戻します。完全不透明な白黒画像はベクトル三角関数を使い、半ピクセルや画像端に近い点は旧スカラーパスで再計算します。連続階調、カラー、透明画像はスカラー三角関数/補間を保ち、3 次ピッチ幾何だけを高速化します。一定ピッチ、解析パターンコールバック、SIMD 非対応ではスカラーを使います。一定ピッチの 4 点パスは現測定で性能低下があり無効です。全て `double` のまま、単精度座標や近似螺旋式は使いません。

生成 API では `MultiBlockSolveOptions.UseSimdSampling = false` でスカラー参照パスを選び、性能/出力を比較できます。既定は `true` で GUI/CLI の変更は不要です。回帰は両パスの payload/件数一致、3 次ピッチの区間境界、半ピクセル、alpha しきい値、両回転方向、並列出力順を検証します。同一入力/LBA 範囲の実 payload バイトで速度を計測し、三角関数単体の高速化を全ディスク速度とみなしません。

一定ピッチの高速逆算は求解後不変の内半径積分値、半径の 2 乗、ピッチ係数をキャッシュし、1 回の Newton 補正で同じ平方根を再利用します。元の `double` 演算順序、除算、境界動作を保ち、近似積分や逆数乗算に置き換えません。独立した旧式回帰は半径、角度、短い螺旋境界をビット一致比較します。`UseSimdSampling` の両方にこのスカラー最適化があるため、性能比較は最適化前後の独立エンジンで行います。

`payload-transition-class-error=0` は制御 payload が全て正しい 2 階調 `0xA5/0x92` クラスに属する意味で、固定フィールド、ECC、SYNC の遷移密度が等しい意味ではありません。JSON の `dispersionPool.levels` は 2 つの入力範囲、Q バイト、遷移数、公称プレビュー輝度を記録します。これらは実ディスクの反射率とは異なります。

出力内容：

- `payload.bin`：16 個の 2048-byte セクターを順に連結；
- `payload.bin.json`：PSN、初期/最終境界、誤差、反復数、経過時間；
- 任意の `channel.bin`：77376-byte にパックした NRZI レベル。

## 書き込み可能な ISO 出力

`solve` は ISO だけを出力するか、32768-byte payload も保持できます：

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2295104 `
  --iso-output solved-disc.iso `
  --lba 0 `
  --iterations 10000
```

画像モードは `--total-sectors` を ISO 論理セクター数として再利用します。テキスト制約には `--iso-sectors` も必要です：

```powershell
--target target.txt --iso-output solved-disc.iso --iso-sectors 2295104
```

新イメージは最小 ISO9660 ファイルシステムと `SOLVED.BIN` を含み、解いた 16 個の 2048-byte セクターを `--lba ... --lba+15` に書きます。LBA 0 は ISO9660 system area なので、ファイルシステムのデータ領域に可視の `SOLVED.BIN` コピーも置きます。スパース対応 NTFS では可能ならスパース保存し、論理長は `total-sectors × 2048` のまま、書き込みソフトから穴は全ゼロに見えます。

既存 ISO の連続 16 セクターを置換することもできます：

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2295104 `
  --iso-template original.iso `
  --iso-output solved-disc.iso `
  --lba 32 `
  --iterations 10000
```

テンプレートサイズは 2048 の整数倍、画像モードではセクター数が `--total-sectors` と同じである必要があります。ディレクトリ、ファイル、UDF/ISO メタデータに割り当てたセクターの置換はファイルシステムを壊すため、予約領域か対象ファイルの既存 extent を選びます。最小新 ISO は固定 ISO9660 volume descriptor セクター 16/17 の上書きを拒否します。

ISO は 2048-byte ユーザーデータを保存し、619008-bit 生チャネルストリームではありません。標準 DVD ドライブは ID、EDC、ECC、EFMPlus、NRZI を再生成するため、実パターンは符号化選択が同じか、ソルバーの初期 state、DSV、pit/land が実書き込み境界と一致するかにも依存します。

### 全ディスクまたは指定セクターの連続生成

`--fill-sectors` が 16 を超えると `--lba` から連続 ECC Block を解き、最終 state、DSV、pit/land を次へ渡します。セクター数は 16 の倍数です。2297888 セクターの全ディスク用コマンド：

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --total-sectors 2297888 `
  --iso-output full-disc.iso `
  --fill-sectors 2297888 `
  --lba 0 `
  --psn-offset 0x30000 `
  --inner-radius-mm 24 `
  --outer-radius-mm 58 `
  --channel-bit-nm 133.3 `
  --constraint-step 16 `
  --algorithm state-control `
  --bridge-bytes 2 `
  --beam-width 8 `
  --retry-beam-width 32 `
  --retry-frame-fraction 0.25 `
  --batch-frames 8 `
  --batch-candidates 4 `
  --search-lanes 1 `
  --iterations-per-block 3 `
  --initial-state 1 `
  --initial-dsv 0 `
  --initial-level land
```

最初の 1600 セクターだけなら次に変更します：

```powershell
--fill-sectors 1600
```

LBA 0～1599 の 1600 セクターを出力します。`--total-sectors` は幾何計算用の全容量のままです。`--lba` がゼロ以外なら前方の全ゼロセクターも保持し、長さは `(lba + fill-sectors) × 2048` です。

範囲モードは ISO9660 記述子を含む指定全セクターを上書きするため、直接書ける生 2048-byte/sector イメージで、マウント可能な ISO9660 ではありません。`--constraint-step` は自由 payload 探索の画像サンプル間隔で、state-control は同じ符号語内の 2 値サンプルを多数決します。最終可視性は間隔に制限されず、派生/固定/SYNC を含む全 16-bit チャネル符号語中心を再サンプリングします。2 値画像のコントラストには各符号語に目標を置く `--constraint-step 16` を推奨します。`32` は速いが半密度、`256` は約 1/16 だけの制約で、全体の majority error は乱数並みになりがちです。全ディスクは 143618 ECC Blocks なので、先に 160～1600 セクターで速度と効果を測定します。

dispersion は常に全 payload 符号語中心でサンプリングし、`--constraint-step` の疎な探索間隔は無視するため省略できます。

## ファイルとパターンの混合モード

`--data-dir` で、ディレクトリ/サブディレクトリを再帰的に標準 ISO9660 + Joliet にする混合モードが有効になります。`--file-position inner`（既定）は内周にファイル、外にパターンを置きます。`--file-position outer` は末尾にファイル内容、先頭メタデータとファイルの間にパターンを置きます。描画境界を 16-sector ECC Block に揃え、全ディスク幾何を保ち、残った描画領域を全体へ引き伸ばしません。Windows などで通常のデータディスクとして開け、Joliet は中国語ファイル名を保持します。

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- solve `
  --image picture.png `
  --data-dir ".\disc-files" `
  --file-position outer `
  --iso-output hybrid-disc.iso `
  --total-sectors 2297888 `
  --volume-label MY_DISC `
  --inner-radius-mm 24 `
  --outer-radius-mm 58
```

既定は `--algorithm dispersion --fast-output true` で、外周全体を直接生成する用途に適します。`--fast-output false` を明示すると内周全ブロックを先に符号化/再生して外周開始時の EFMPlus state、DSV、pit/land を求め、厳密境界で描画します。内周データが多いほど再生時間が増えます。

GUI のデザインの「ディスクに含めるファイル」でフォルダーと内周/外周を選択し、エクスポートと共有、プロジェクトに保存し、ストリーミング書き込みにも使います。ストリーミングは高速 dispersion 固定です。走査で配置を確定し、メタデータ、ファイル、パターンをディスク順に出力して完全な一時 ISO を作りません。外周配置なら先にパターン、後でファイルです。計画後に元ファイルサイズが変わると続行を拒否します。ファイルシステム領域を黄色でプレビューしますが、色は書き込みデータに入りません。

`--lba`/`--fill-sectors` は受け付けず、配置と容量から計算します。`--drawing-start-lba N` は既定開始点より後に余白を設けられますが、描画可能域内で `--psn-offset` と合わせて 16 セクターに揃えます。`--file-position outer` には `--data-dir` が必要です。

ISO9660 は再帰ディレクトリと Joliet Unicode 名をサポートし、単一ファイルは 4 GiB 以下です。リンク循環やフォルダー外の意図しない参照を防ぐため、`--data-dir` は再解析ポイント/シンボリックリンクを拒否します。

## 半径キャリブレーションプレビュー

CD 版の順方向投影に倣い、全体チャネル位置の順に、生成時に想定する螺旋上で画像を取り、同じ位置を推定実螺旋に投影します。同じ出力ピクセルに重なるトラックサンプルは平均し、ピッチ差の累積位相を保ち、各出力ピクセルから最近トラックを逆算する泡状モアレを避けます。

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- calibrate `
  --image picture.png `
  --output calibration-preview.png `
  --total-sectors 2297888 `
  --generated-inner-radius-mm 24 `
  --generated-outer-radius-mm 58 `
  --actual-inner-radius-mm 24.2 `
  --actual-outer-radius-mm 57.8 `
  --channel-bit-nm 133.3 `
  --spiral-direction ccw `
  --image-threshold 128 `
  --alpha-threshold 1 `
  --preview-size 2048 `
  --samples-per-sector 16
```

`--samples-per-sector` は順方向投影密度で範囲 1–4096、既定 16 です。DVD のセクターは約 5.16 mm のトラックで、16 なら約 0.32 mm 間隔です。32/64 は空ピクセルを減らし詳細を増やしますが、時間はほぼ比例増です。任意回転オフセットはなく、生成/実螺旋は 0° 始点を共有し、全体回転は半径キャリブレーションに影響しません。`calibration-preview.png.json` も出力し、`mappingMode` は `forward-channel-splat-average`、密度、両ピッチ、総巻数、軌長を記録します。残存ソルバー誤差やドライブ実装差は含みません。

既定で全ディスクをプレビューします。部分生成なら求解と同じ `--lba`/`--fill-sectors` を与え、未書き込み領域を透明に保ちます。

## 生成 ISO の読み戻しシミュレーション

`calibrate` は元画像から「生成幾何で書き、実測幾何で読む」理想結果を示します。生成と同じ 2 値境界 128 を使い、`--image-threshold` は互換用でプレビューを変えません。`simulate` は生成済み ISO 自体を読み、サンプル payload を DVD スクランブルして `0xA5/0x92` に分類、0/255 として、実測半径螺旋で符号語中心を投影します。旧多階調用に `0x66` は 0、`0xB9` は 85 で表示しますが、新規生成には使いません。cdimage キャリブレーションダイアログと同じ閉ループで、生成幾何は ISO に固定済みなので実測幾何だけで模擬できます。

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- simulate `
  --iso drawing.iso `
  --output readback-preview.png `
  --inner-radius-mm 24.2 `
  --outer-radius-mm 57.8 `
  --preview-size 2048 `
  --samples-per-sector 16
```

`--total-sectors`、`--lba`、`--fill-sectors` の省略時は求解の `drawing.iso.json`（描画開始 LBA/セクター数）を読み、なければ LBA 0 から ISO 全体を使います。`--samples-per-sector` はセクターごとの payload サンプルバイト数（1–2048、既定 16）です。スクランブル後が 2 値表/旧 66/B9 にないバイトは除外します。先頭ファイルシステムが偶然一致する場合があるため sidecar の描画範囲で除外します。ISO に PI/PO 訂正コードは入りません。出力横の `readback-preview.png.json` は `mappingMode` を `payload-scramble-classify-splat` とし、一致数、ピッチ、総巻数を記録します。

生成の `FastDispersionImageWriter` は `PayloadDirectChannelOffset + 8`（16 ビット符号語の半分）で画像目標を中心に置き、読み戻しも同じ位置を使うため、8 チャネルビット（約 1 µm 軌長）のずれはありません。両者は螺旋、方向、スクランブルマスクも共有するので、生成/プレビューのパラメーターが同じならサンプルごとに再現します。

ISO 切り替え時、プレビューは sidecar の `imageMapping.totalSectors`、`innerRadiusMm`、`outerRadiusMm` を読んでキャリブレーション幾何をロードします。前のディスク寸法で外部イメージを細い環に圧縮せず、その後の微調整もロードした値を起点にします。

## プレビューワークスペースの 2 モード

入力スイッチで両経路を選び、デバウンス、キャンセル、エクスポートを共有します：

- **生成物の読み戻し**（読み戻し模擬、`simulate`）は生成 ISO と 1 組の実測幾何を入力し、実際に書かれたバイトを表示します。
- **デザイン投影**（キャリブレーションプレビュー、既定、`calibrate`）は元画像と生成/実測幾何を入力します。ISO を読まず変更後の更新は速い一方、実測幾何での元画像投影であり、実符号語ではなく、ソルバー誤差やファイルシステム接頭部は表示しません。

キャリブレーションプレビューは生成と同じデザイン合成画像を直接使うため、両方の図柄は一致します。

## 順方向符号化だけを実行

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- encode `
  --input payload.bin `
  --output channel.bin `
  --lba 0 `
  --psn-offset 0x30000 `
  --initial-state 1 `
  --initial-dsv 0 `
  --initial-level land
```

先頭 PSN は 16 の倍数である必要があります。そうでなければ ECC Block 境界から始まらないため拒否します。

## 変調テーブルの再生成

`Encoding/EfmPlusTables.Generated.cs` は ECMA-267 Annex G から機械抽出したものです。規格 PDF から再生成するには：

```powershell
python tools/generate_efm_tables.py ECMA-267.pdf src/DvdImageSolver/Encoding/EfmPlusTables.Generated.cs
```
