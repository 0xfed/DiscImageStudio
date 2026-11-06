# 上游整合后的标定流程

[简体中文](CALIBRATION_TUTORIAL.md) | [繁體中文](CALIBRATION_TUTORIAL.zh-TW.md) | [English](CALIBRATION_TUTORIAL.en.md) | [日本語](CALIBRATION_TUTORIAL.ja.md) | [Türkçe](CALIBRATION_TUTORIAL.tr.md)

辅助标定现在提供粗标、边界标定和精标三个阶段，并可独立于绘图工程使用。先确认盘片容量，再为每张空白盘新建对应阶段的标靶；每张已刻盘都必须使用生成时保存的记录和照片。

圆环直径、宽扇区和 DVD 目视选择器继续可用。新记录明确保存图案来源；本分支原有 schema 7–9 无图案来源字段的记录按原始图案解释。不要通过手改版本号转换上游旧记录。

粗标使用三条宽带及角度标记，取得下一张盘的半径初值。边界标定沿清楚的黑白交界分段取点；精标沿同一条十字臂描点，必要时拟合三次轨距。每轮核对预测曲线后再应用结果，并保存记录。

照片矫正使用实体盘片外缘和中心孔。12 cm 盘的实体外半径为 60 mm，不能用刻录区外半径替代。每轮使用新的空白盘；重复计算同一张照片不会生成新的物理测量。

## 可打开的 Verbatim AZO DVD-R 示例

这些记录包含照片、矫正参数和测量点。它们展示一次实盘流程，不能保证其他盘片、刻录机或批次的精度。

- [AngleSweepBands](examples/verbatim-azo-20261104/calibration-4f0228e6.calibration.json)
- [WavyBoundary](examples/verbatim-azo-20261104/calibration-3f2ce8c2.calibration.json)
- [ReferenceCross](examples/verbatim-azo-20261104/calibration-14580aac.calibration.json)

[完整的上游中文实操教程和历史数值见此固定版本。](https://github.com/JiaFeiMiao-K-Cat/DiscImageStudio/blob/c720fd45b76dc4d9257316474dd646a91589ffdb/docs/CALIBRATION_TUTORIAL.md)
