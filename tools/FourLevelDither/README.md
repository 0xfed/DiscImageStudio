# 黑白抖动工具

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [日本語](README.ja.md) | [Türkçe](README.tr.md)

独立的 Windows 图片处理程序，将原图转换为只含 **0、255** 的黑白二阶 PNG。DVD 和 CD 都使用这两档，通过黑白像素的空间混合保留明暗。可执行文件名称继续使用 `FourLevelDither.exe`，以兼容现有入口。

## 窗口操作

1. 运行 `FourLevelDither.exe`，点击“打开图片”或将图片拖入窗口。
2. 选择“DVD 二阶”或“CD 二阶”，查看对应的码字说明；两种类型都输出相同的黑白调色板。勾选“100% 像素查看”可检查抖动细节。
3. “sRGB 伽马校正”默认关闭，保持已有输出；勾选后自动重新处理，窗口会显示当前开关状态。
4. 点击“保存黑白 PNG”，另存处理后的图片。关闭校正时文件名默认以 `-黑白抖动.png` 结尾，开启时为 `-黑白抖动-sRGB.png`，便于对比。程序拒绝覆盖当前原图路径。

切换光盘类型或伽马选项都会从缓存的原图重新计算，不会对上一次抖动结果再次处理。由此前三阶或四阶方案改为二阶时，请打开未抖动的原图重新处理，以保留最完整的明暗信息。

支持 PNG、JPEG、BMP、TIFF、GIF 等系统图片解码器支持的格式。导出为无损 Gray8 PNG，保留原图像素尺寸和有效 DPI；无效 DPI 使用 96。预览缩放不会改变保存的像素尺寸。

## 处理规则与光盘映射

默认情况下，颜色按 Rec.709 权重在编码 RGB 空间转换为亮度，透明部分先合成到白色背景，再使用**蛇形 Floyd–Steinberg 误差扩散**：相邻行交替扫描，为带扩散误差的亮度选择最近的黑或白，再将亮度差分配给后续像素。

| 主程序二阶分档范围 | PNG / 预览灰度值 | DVD 扰码域 Q byte | CD byte |
| --- | ---: | --- | --- |
| 0–127 | 0 | `0xA5` | `0x0D` |
| 128–255 | 255 | `0x92` | `0xAA` |

默认关闭伽马校正时，原图灰度 128 的大块区域会形成约 `128 / 255 = 50.2%` 的白色像素，其余为黑色。抖动期间会叠加扩散误差；无论是否开启伽马校正，输出文件都只包含 0 和 255。

### sRGB 伽马校正

开启后，程序先将每个 RGB 通道按标准 sRGB 传递函数解码为线性亮度，再使用 Rec.709 权重计算亮度、在线性空间合成白底并扩散误差。它改变黑白像素的分配比例，不改变光盘码字或输出灰阶。

例如原图灰度 128 解码后约为 21.6% 的线性亮度，因此会产生约 21.6% 的白色像素；同一张图的中间调通常会比关闭时更深。可保存两个版本比较实盘效果。关闭选项仍按原有算法计算，默认行为保持不变。

主程序的影调在设计页右栏统一设置，生成、流式刻录及设计投影共用所选影调；无需先在独立工具中导出 PNG。新建工程默认抖动强度 75%、2 级、亮度 0，sRGB 线性化关闭；CD 可选 2/4 级，DVD/BD 使用 2 级。生成几何在工程设置中编辑。

主程序当前按图层进行设计合成，不再提供旧版 CD/DVD 环形复制选项。独立工具只处理单张图片，不负责工程图层布局。

处理核心 `DiscImageStudio.Imaging.GrayscaleDither` 仍支持任意有序灰度调色板。旧 `FourLevelDither` API 仅为现有码字探针等工具保留兼容行为，独立窗口与 CLI 均使用黑白二阶。

## 命令行

窗口与命令行均支持简体中文、繁体中文、英文、日文和土耳其文，语言在启动时确定。使用 `--language zh-CN|zh-TW|en|ja|tr|system` 显式选择语言；未指定时使用环境变量 `DISC_IMAGE_STUDIO_LANGUAGE`，否则跟随系统界面语言。语言选择不改变模式值、文件名或像素输出。

```powershell
# 无窗口转换，默认 DVD 二阶；输出必须是 .png，且不能已经存在
.\FourLevelDither.exe --input original.jpg --output result.png

# 显式选择 DVD 或 CD；两者均输出 0/255 黑白二阶
.\FourLevelDither.exe --input original.jpg --output dvd-result.png --mode dvd
.\FourLevelDither.exe --input original.jpg --output cd-result.png --mode cd

# 对比关闭与开启 sRGB 伽马校正；省略 --gamma 等同于 none
.\FourLevelDither.exe --input original.jpg --output result-none.png --gamma none
.\FourLevelDither.exe --input original.jpg --output result-srgb.png --gamma srgb

# 带入一张图片并打开窗口
.\FourLevelDither.exe original.jpg

# 查看帮助（也支持 --help）
.\FourLevelDither.exe -h

# 用日文打开窗口
.\FourLevelDither.exe --language ja original.jpg

# 用土耳其文进行无窗口转换
.\FourLevelDither.exe --language tr --input original.jpg --output result-tr.png
```

## 从源码运行与发布

在仓库根目录使用 .NET 10 SDK：

```powershell
dotnet run --project tools/FourLevelDither --configuration Release

dotnet publish tools/FourLevelDither/FourLevelDither.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/FourLevelDither/win-x64
```

自包含发布的 `FourLevelDither.exe` 内置 .NET 10 运行时；直接使用非自包含构建产物时，需要安装 .NET 10 Windows 桌面运行时。
