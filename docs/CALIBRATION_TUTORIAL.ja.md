# 上流の統合後の校正手順

[简体中文](CALIBRATION_TUTORIAL.md) | [繁體中文](CALIBRATION_TUTORIAL.zh-TW.md) | [English](CALIBRATION_TUTORIAL.en.md) | [日本語](CALIBRATION_TUTORIAL.ja.md) | [Türkçe](CALIBRATION_TUTORIAL.tr.md)

補助校正には粗校正、境界校正、精密校正の段階があり、描画プロジェクトとは独立して使用できます。ディスク容量を確認し、空のディスクごとに対応する段階のターゲットを作成してください。記録済みディスクの解析には、その生成時の記録と写真を使います。

直径測定、広い扇形、DVD の目視セレクターも引き続き利用できます。新しい記録には図案の系統を明示します。この分岐の schema 7–9 で図案フィールドのない記録は元の図案を保持します。上流の古い記録のバージョン番号を手動で変更しないでください。

粗校正では幅広いバンドと角度マークから次のディスクの半径初期値を求めます。境界校正では明瞭な白黒境界を区間ごとになぞり、精密校正では同じ十字線の腕をなぞって必要に応じて 3 次のピッチを近似します。予測曲線を確認してから結果を適用し、記録を保存してください。

写真は物理的なディスク外縁と中心穴に合わせて補正します。12 cm ディスクの物理的な外半径は 60 mm であり、記録領域の半径とは異なります。各反復には新しい空のディスクを使ってください。同じ写真の再計算は新しい物理測定にはなりません。

## 開ける Verbatim AZO DVD-R の記録例

これらの記録には写真、補正パラメーター、測定点が含まれます。一度の実機での手順を示すもので、他のメディア、ドライブ、製造ロットの精度を保証するものではありません。

- [AngleSweepBands](examples/verbatim-azo-20261104/calibration-4f0228e6.calibration.json)
- [WavyBoundary](examples/verbatim-azo-20261104/calibration-3f2ce8c2.calibration.json)
- [ReferenceCross](examples/verbatim-azo-20261104/calibration-14580aac.calibration.json)

[上流の中国語による詳しい手順と過去の測定値は、固定リビジョンで参照できます。](https://github.com/JiaFeiMiao-K-Cat/DiscImageStudio/blob/c720fd45b76dc4d9257316474dd646a91589ffdb/docs/CALIBRATION_TUTORIAL.md)
