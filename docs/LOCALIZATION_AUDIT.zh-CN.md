# 国际化审计

[简体中文](LOCALIZATION_AUDIT.zh-CN.md) | [繁體中文](LOCALIZATION_AUDIT.zh-TW.md) | [English](LOCALIZATION_AUDIT.md) | [日本語](LOCALIZATION_AUDIT.ja.md) | [Türkçe](LOCALIZATION_AUDIT.tr.md)

更新于 2026-11-04。本审计涵盖仓库自有的界面、CLI、诊断和文档，语言为简体中文、繁体中文、英文、日文和土耳其文。已补齐本次识别的翻译缺口；Windows CI 现已验证运行时国际化与布局矩阵。资源存在性与结构检查本身不能证明语义准确。

## 覆盖与复核

| 范围 | 证据 |
| --- | --- |
| 共用资源 | 2053 条资源在五语中均非空，无重复键或字段；嵌入资源与源码一致 |
| 源码引用 | 2077 处静态／动态 C# 与 XAML 引用可解析，涵盖预设、阶段、预览标题、标定指导和对话框过滤规则 |
| 界面语义 | 逐语复核全部 749 条既有界面资源：Common 154、Workflows 239、Workspace 151、Xaml 205 |
| CLI 与诊断 | 桌面／跨平台 CLI、独立 DVD 引擎、Core、CD/DVD/BD、ImageIO、Imaging、ISO/UDF 与刻录层的自有帮助、错误、进度和结果使用共用资源 |
| 工具 | 三个 C# 工具、六个 Python 工具、四个 PowerShell 打包／安装脚本支持五语文案与语言选择 |
| 文档 | 全部 23 份源文档均有五语覆盖，包括 README、完整变更历史、技术指南、政策和本审计 |

语义复核恢复了操作动词、限定条件、刻录机实际采用的速度设置、反射亮度、包含 60% 边界的覆盖率、标定记录用语、仅删除选中点、输出文件夹用途及双向缩放。繁体中文交织术语与语言自称已统一。长标签、标题和标定按钮可换行增高，并保留紧凑按钮最小高度；Windows CI 布局检查现已通过。

另一轮源码清点追踪了未包装的中日韩／英文短语候选和 XAML 显示属性到其产生位置。Core 拟合警告仅在显示时翻译，分类用原始文本不变。已区分保存的用户默认值、夹具名称、协议字段、设备详情与说明性文字。该清点发现并修复七条 DVD 流式错误及空白禁用预设的上下文；运行时回归覆盖全部语言，源码守卫也覆盖 DVD 适配层异常字面量和桌面／工具 XAML 显示属性。

已复核界面资源指纹采用 UTF-8 与统一换行。它们标识复核版本，不是语义准确性的自动证明。

| 资源文件 | SHA-256 |
| --- | --- |
| `Common.json` | `7c304b1575bf7d3494c0f73032f45524e4439ce43f8bef24225041fabac971bf` |
| `Workflows.json` | `2a27cd6337ff55c4841271ad851da0b7c4bb6abede365d710714a210d9f32112` |
| `Workspace.json` | `b389bbf7a35a094f1b405e6e475b7af953a0ff045d32bfb096d465fc7063ba07` |
| `Xaml.json` | `f3728ada2da11e6be5614e2733e94e9b2e8ae0d448eedd7b6bb5bfae2250ed6f` |

## 保留的行为

各命令行入口共用全局语言选项与环境偏好。已覆盖地区／系统回退、无效／缺值／重复选项、UTF-8 输出与运行时界面语言选择。只修改 UI culture，不改变数值文化或参数解析；缓存的模块说明跟随语言切换。启动错误资源独立加载，使主资源失败及 CLI 错误包装不会递归。

命令、选项名、schema 字段、技术 ID、选项值、默认文件名、贡献者、用户填写的名称／说明、路径及保存的几何不变。操作系统／第三方原始详情与官方许可证原文保留。首次用户预设仅在盘型、ID、未修改文本都匹配时于显示阶段翻译。西班牙语有 24 条未启用的部分资源，不属于五种受支持语言。

编码决定和二进制输出不依赖翻译。刻录显式携带收尾标记。测试验证工程数据、CD 音频／数据、DVD EFMPlus／ISO、BD ISO／探针、ISO9660/Joliet/UDF 字节、数值分析与生成 C# 表在不同语言间一致；不由此推断实盘效果。

## 文档完整性检查

清单记录规范源文件的哈希与各语言路径。检查保留标题、段落、列表项、表格尺寸与数值、行内代码、命令／公式、引用目标和本地链接。明确例外仅允许预设示例名称／说明及两个 Mermaid 显示标签翻译，仍保留 JSON 类型／精度／ID／贡献者、图示节点／连线／方向／数字。缺失或空白标签、重复 JSON 字段、无效数值会失败。

用于发布的双语日志保留原结构；明确声明的并列列表必须有相同条目数与技术标识，各语言阅读版对相同改动仅展示一次。全部 120 条独立记录、历史日期及原验证限制保留。生成报告不计入源文档；仅四条明确登记、源码已引用且位于忽略 artifacts 目录下的输出可在干净检出中缺失，其他本地链接必须存在。

全仓库文档检查报告 23/23，并在 CI 强制执行。七项规则回归保护这些约束。结构检查辅助语言复核，源文件修改后必须重新复核。维护与 Windows 截图命令见 [LOCALIZATION.md](LOCALIZATION.md)。

## 验证与限制

启用 Windows 目标的 Release 全解决方案构建零警告、零错误。共享国际化回归通过，涵盖实际五语 DVD 流式错误、异常类型／参数名、保存数据、资源加载失败释放与数值文化。跨平台 CLI 包冒烟、DVD 自检、C# 蓝光探针通过；探针对照 256 条独立字节向量与有限模式。假刻录机验证字节、MMC 命令／超时、进度、取消、清理，不访问硬件。

PowerShell 通过 45 项既有发布回归及 69 项安全国际化预检，涵盖 47 个键、元数据不变、Unicode 路径、子进程语言传递；不构建、签名、安装或提权。Python 检查 134 个键，六工具均对照原始数值／schema 基准通过集成。参考搜索至周期 8，三类图表的五语字形／边界检查通过，人工检查五幅代表图。夹具为合成数据，含故意无效图集，不是物理测量或完整生产探针扫描。

Windows CI 已通过 WPF 语言切换、字形／布局截图、抖动界面／PNG 集成、DVD 图集／预览集成及 Windows PowerShell 预检。包安装路径仍未执行。这些测试不确立真实刻录机性能或光学亮度；它们是验证限制，不代表新增硬件验证。

## 复现检查

```sh
dotnet run --project tests/DiscImageStudio.LocalizationTests -c Release
dotnet run --project tests/DiscImageStudio.Burning.LocalizationTests -c Release
python3 tests/test_cli_package.py /absolute/path/to/disc-image-studio
python3 tests/test_probe_localization.py /absolute/path/to/BluRayByteProbe.dll
python3 -m unittest discover -s tests -p test_python_localization.py
python3 tests/test_python_tool_localization.py
python3 -m unittest discover -s tests -p test_documentation_translation_rules.py
python3 tests/test_documentation_translations.py --require-complete
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

Windows 上还应运行运行时套件并检查各语言截图：

```powershell
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release
```

独立 Windows CI 进程检查五语共 325 个布局场景：CD/DVD/BD 工作区的 1366×900 深色与 960×700 浅色布局、展开的预览参数与读回、标定面板、设置分类及新建／编辑工程对话框。检查语言切换后已加载的页面标题、实际绘制的文字边界与可见裁剪区域及缺失字形，允许有意使用的省略号和最近工程路径滚动。临时存储与不可写盘的假刻录机隔离夹具，并检查工程字节、几何与数值文化不变。报告记录实际 DPI，不模拟其他显示器。

```powershell
$env:DISC_IMAGE_STUDIO_UI_SNAPSHOTS = "artifacts/ci/localization-layout"
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release -- --localization-layout
```

运行器也可在本 Linux 工作区构建；Windows CI 已完成全部 325 个场景，问题数为零。即使检查失败，CI 也上传 PNG 与 JSON 报告。空工程属于合成夹具；该矩阵不验证物理光学效果，也不涵盖所有填入数据后的流程状态。
