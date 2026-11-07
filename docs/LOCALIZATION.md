# 国际化

[简体中文](LOCALIZATION.md) | [繁體中文](LOCALIZATION.zh-TW.md) | [English](LOCALIZATION.en.md) | [日本語](LOCALIZATION.ja.md) | [Türkçe](LOCALIZATION.tr.md)

点击右上角常驻的「齿轮 + 设置」按钮，在「通用 → 语言」选择「跟随系统」「简体中文」「繁體中文」「English」「日本語」或「Türkçe」，保存后立即生效。主页和所有工作区都显示设置按钮，也可按 Ctrl+, 打开；取消、Escape 或关闭设置窗口不会保存语言。切换语言不会自动重启应用，也不会关闭工程或中断任务。

`settings.json` 中的 `language` 使用 `system`、`zh-CN`、`zh-TW`、`en`、`ja` 或 `tr`。语言与主题默认均为跟随系统；缺失、空值和不支持的语言也回落到跟随系统，已有配置中有效的语言和主题选择保持不变。跟随系统使用启动时的系统 UI 语言：`zh-TW`、`zh-HK`、`zh-MO`、`zh-Hant` 及显式 `zh-Hant-*` 使用繁体中文；`zh-CN`、`zh-SG`、`zh-Hans` 等其他中文系统使用简体中文；`ja`、`ja-JP` 等日文系统使用日文；`tr`、`tr-TR` 等土耳其语系统使用土耳其语；英文、法文等其他系统统一使用英文。运行期间修改系统语言须重启应用才生效。主题使用 Windows 的应用颜色模式选择深色或浅色。语言独立于 `.discproj`，工程名、用户预设名、图层名、文件路径与数值解析规则不参与翻译。

## 添加和维护文案

翻译位于 `src/DiscImageStudio.Localization/Catalogs/*.json`，构建时嵌入程序集，无额外运行时依赖。每个键包含 `zh-CN`、`zh-TW`、`en`、`ja` 和 `tr` 五种文本；繁体中文使用独立资源，采用「設定」「專案」「檔案」「燒錄」等用语，不在运行时转换用户数据：

```json
{
  "示例 {0}": { "zh-CN": "示例 {0}", "zh-TW": "範例 {0}", "en": "Example {0}", "ja": "例 {0}", "tr": "Örnek {0}" }
}
```

C# 用 `L.T("源文案")`，插值用 `L.F($"示例 {value}")`，资源键为其 .NET 复合格式 `示例 {0}`；需要显式资源键时可用 `L.Format(key, arguments)`。翻译可调整参数顺序，但必须保留索引和格式说明符。XAML 使用 `xmlns:loc="clr-namespace:DiscImageStudio.Localization"` 与 `{loc:Text ResourceKey}`；`TextExtension` 在加载时按当前语言写入，并在语言切换时原地重写已加载的控件和模板。

共享短语只需定义一次，所有资源文件中的键必须唯一。找不到当前语言时回落到中文；键不存在时显示键本身。启动时先初始化语言，再创建窗口及 ViewModel。只设置 UI culture，保留 numeric culture；不要翻译协议标识、命令、路径、用户数据或把翻译后的文本用于逻辑判断。

目标是让五种受支持语言对本仓库自有的界面、命令行帮助、诊断和文档保持 1:1 语义对应。共享运行时与资源位于 `src/DiscImageStudio.Localization`，桌面和 CLI 使用同一份资源。CLI 帮助、模块与选项说明、CD 模块提示、DVD 模块状态与命令路由错误已接入；蓝光、图片加载、Core、CD 和 ISO/UDF 的自有异常，以及蓝光进度摘要、说明性 JSON 和照片校正提示也已翻译，机器标识、几何数值和二进制输出保持不变；覆盖范围及已执行／待执行验证见 [LOCALIZATION_AUDIT.md](LOCALIZATION_AUDIT.zh-CN.md)。第三方、操作系统异常详情以及官方许可证原文保持不变。

## 命令行语言

命令行支持全局选项 `--language zh-CN|zh-TW|en|ja|tr|system`，可放在命令前后，也可使用 `--language=ja`。显式选项优先于环境变量 `DISC_IMAGE_STUDIO_LANGUAGE`，否则跟随系统界面语言。不支持的显式语言、重复选项和缺少值会返回退出码 1。该选项由独立 DVD 程序、跨平台 CLI 及 Windows 应用的命令行入口共用；GUI 无命令启动时仍使用已保存的界面设置。

```sh
disc-image-studio --language ja --help
disc-image-studio cd-generate --language tr --input picture.png --output track.wav --sectors 2
```

只翻译显示文案，不翻译命令名、选项名、协议标识、文件路径、用户输入或 JSON 字段名。工程参数仍按各自既有规则解析；选择语言不会修改数值文化。CLI 关闭 invariant globalization，以便支持命名的 UI 文化。

标定拟合的曲线点指导信息在界面显示时翻译，Core 返回值仍保持原文，自动搜索判断不依赖翻译。保存预设时，自动生成的几何说明保留可移植的原始文本与格式资源键；显示时按当前语言和数值文化重新格式化，手工编辑的说明保持原文。

## 验证

不依赖 WPF 的完整性检查可以在 Windows、Linux 和 macOS 上运行，CI 的各平台都会执行：

```sh
dotnet run --project tests/DiscImageStudio.LocalizationTests -c Release
```

该检查验证五种语言的非空文本、重复键、复合格式与数字/日期格式、文件对话框过滤规则、C# 字面量和插值调用、XAML 文案与模板触发器、内置预设、标定动态指导信息，以及用户预设回退。它也检查缓存模块说明随语言切换、异常类型与参数名，以及工程、ISO9660/Joliet 和 UDF 输出的语言间一致性。它不能证明翻译在语义上完全准确，也不能代替 Windows 上的运行时切换与布局截图检查。

文档翻译的段落结构、列表、命令示例、链接与源文件版本可以单独检查：

```sh
python3 tests/test_documentation_translations.py
python3 tests/test_documentation_translations.py --require-complete
```

文档检查同时保留段落、表格行列与数值（允许本地化小数和千分符）。第一条只验证清单中已完成的文档并报告未完成项；第二条是全仓库文档完整性检查，存在未完成项时失败。当前检查结果与待办见 [LOCALIZATION_AUDIT.md](LOCALIZATION_AUDIT.zh-CN.md)。

CI 强制执行全仓库完整性检查。

清单可显式允许预设 JSON 示例翻译 `displayName` 与 `description`；字段名、类型、数值精度、ID 及贡献者仍必须一致，重复字段和无效数值会被拒绝。

生成的测试报告不计入源码文档。清单可用 `generated_artifacts` 显式登记可重建的结果链接；仅允许源码中已引用且位于忽略的 artifacts 目录下的路径，其他本地链接仍须存在。

清单可用 `translated_mermaid_labels` 列出可翻译显示文字的 Mermaid 节点；节点 ID、连线、方向、其他标签与标签中的数字保持一致，缺少或空白的标签会被拒绝。

发布源日志保留中文与英文并列小节；清单可用 `parallel_language_sections` 显式登记这两个标题，供各语言阅读版只展示一次相同改动。每个并列版本必须是完整项目列表且条目数、内联技术标识一致，缺失小节或额外未列项目会被拒绝；历史子标题与其他正文保持完整。

刻录层的异常、进度、操作说明和设备诊断也使用共享资源。`OpticalBurnOperation.IsFinalizing` 显式携带收尾状态，不能通过翻译后的文字判断是否封盘。以下回归不依赖 WPF，使用实际刻录库配合假设备，在五种语言下检查 CD/BD 字节、MMC 命令与超时、进度阶段、取消令牌和清理行为；不访问真实刻录机，也不执行物理刻录。

```sh
dotnet run --project tests/DiscImageStudio.Burning.LocalizationTests -c Release
```

PowerShell 打包与安装脚本通过 `packaging/Localization.ps1` 读取共享资源，支持原生 `-Language` 参数及环境偏好，并将所选语言传给子检查和提权后的安装进程。元数据、日志正文、身份、路径、校验值和数值文化保持不变。以下测试只运行元数据和预检失败路径，使用模拟子进程，不构建、签名或安装包，也不请求提权。Windows PowerShell 可将 pwsh 换为 powershell。

```powershell
pwsh -NoProfile -File tests/Test-ReleaseTooling.ps1
pwsh -NoProfile -File tests/Test-PackagingLocalization.ps1
```

C# 资源在选定语言后延迟加载；空资源和重复键诊断直接读取独立的嵌入式错误资源，避免在主资源加载过程中递归调用翻译器。共享运行时回归验证五语诊断、原始资源名、异常类型、流释放和数值文化。 CLI 错误前缀也从该独立资源读取，确保入口错误处理不会重试已失败的主资源加载；Python 错误包装使用相同方式。

Python 工具通过 `tools/localization.py` 读取同一份 JSON 资源，也共用全局语言选项与环境偏好。解析器的帮助与错误、分析说明和图表标注支持五语，不修改数值 locale、字段名、路径或算法。标准库检查审核所有 Python 工具及 argparse 格式参数；集成检查需要 NumPy 与 matplotlib，使用最大周期 8 的参考搜索、合成图表和故意无效的图集，核对原始脚本的数值与 schema 基线、源文件、字体及图表文字边界，也验证生成的 C# 表代码不随语言改变。这些夹具不是实盘测量或完整探针扫描。

```sh
python3 -m unittest discover -s tests -p test_python_localization.py
python3 tests/test_python_tool_localization.py
```

`LocalizationTests` 检查资源语言完整性、格式参数、系统语言回落、设置保存和取消行为，以及保存失败不会应用语言；`RelocalizationTests` 检查运行时切换语言后工程几何、进行中的读取、预览结果和已打开页面的文案保持不变或正确刷新。

```powershell
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language en --output artifacts/settings-en.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language zh-CN --output artifacts/settings-zh.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language zh-TW --output artifacts/settings-tw.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language ja --output artifacts/settings-ja.png
dotnet run --project src/DiscImageStudio.App -c Release -- ui-snapshot --settings-dialog --language tr --output artifacts/settings-tr.png
```

截图使用一次性设置文件，不改变用户配置。增加语言时同时扩展语言规范化和解析规则、设置下拉选项、各资源及测试。

独立 Windows CI 进程检查五语共 325 个布局场景：CD/DVD/BD 工作区的 1366×900 深色与 960×700 浅色布局、展开的预览参数与读回、标定面板、设置分类及新建／编辑工程对话框。检查语言切换后已加载的页面标题、文字宽高与缺失字形，允许有意使用的省略号。临时存储与不可写盘的假刻录机隔离夹具，并检查工程字节、几何与数值文化不变。报告记录实际 DPI，不模拟其他显示器。

```powershell
$env:DISC_IMAGE_STUDIO_UI_SNAPSHOTS = "artifacts/ci/localization-layout"
dotnet run --project tests/DiscImageStudio.ArchitectureTests -c Release -- --localization-layout
```

运行器已在本 Linux 工作区编译；Windows 执行仍等待推送后的仓库 CI。即使检查失败，CI 也上传 PNG 与 JSON 报告。空工程属于合成夹具；该矩阵不验证物理光学效果，也不涵盖所有填入数据后的流程状态。

新的上游设备诊断资源可分批翻译，缺少当前语言时回退到英文。已完成的界面资源继续要求五语覆盖；设备字段名、数值格式和原始操作系统错误保持不变。
