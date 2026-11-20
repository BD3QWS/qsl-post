# QSL 邮务台

原生 Windows QSL 卡片收发、封发、交接、物流追溯与单据输出软件。首次启动建立空台账，可在设置中登记个人台站或卡片局。

[下载 Windows 安装包](https://github.com/BD3QWS/qsl-post/releases/latest)

## 运行

安装器会请求管理员权限。用原来的 Windows 账户升级可沿用已有安装目录与台账。程序依赖 .NET Framework 4.0 Full 或兼容的 4.x 运行库。程序构建为 x86、PE subsystem 4.00。当前测试主机为 Windows 11 x64，XP SP3 属兼容构建目标，XP 与 Windows 10 设备尚待验证。

在「设置与备份」可更改资料和查询缓存目录。按 F1 或使用 `--manual` 打开使用手册。可用 `--data "绝对目录"` 指定独立资料库。同一资料库仅允许一个写入进程。详细步骤见 [使用说明](docs/使用说明.txt)。

## 构建

使用 Windows PowerShell 以及 Windows .NET Framework C# 编译器。提供 .NET Framework 4.0 **reference assemblies**（不是用本机 4.8 的实现程序集代替），例如 Microsoft.NETFramework.ReferenceAssemblies.net40 包中的 `build/.NETFramework/v4.0` 目录。

```powershell
.\Build.ps1 -FrameworkRefs 'C:\deps\net40\build\.NETFramework\v4.0' -OutputDir '.\build\release'
.\tests\Run.ps1 -FrameworkRefs 'C:\deps\net40\build\.NETFramework\v4.0'
.\installer\BuildInstaller.ps1 -Payload '.\build\release' -Makensis 'C:\tools\nsis\makensis.exe'
```

安装包输出到 `outputs/1.4.4/QSLPost-Setup-1.4.4.exe`。

默认构建把通用名址示例嵌入使用手册。可以通过 `-GuideExample` 指定自用的 XML 文件，将该文件保存在仓库之外。根节点为 `Contact`，可填写 `Callsign`、`Name`、`Address`、`PostalCode`、`Phone`、`Email`、`EnglishName` 和 `EnglishAddress`。

构建时会用到参考程序集、NSIS 和官方 WebView2 SDK。台账与打印只依赖 .NET Framework 和打印机驱动。内置免费查询使用 Windows 10／11 和 Microsoft Edge WebView2 Runtime，浏览器查询由系统默认浏览器打开。

## 实现

- `src/Domain.cs`：永久编号、XML 事务保存、单写入锁、状态迁移、跨袋转寄、备份和完整性历史。
- `src/StorageLocations.cs`：资料和查询缓存目录设置、资料迁移。
- `src/Exchange.cs` 与 `tracking/`：CSV / ADIF 导入导出、内置 17TRACK 免费查询及物流进度记录。
- `src/Documents.cs` 和 `src/PagePreview.cs`：原尺寸页、自动续页、Code 128、最高 600 dpi 无损 PDF 和可缩放预览。
- `src/UpuForms.tsv`、`src/UpuForms.cs`：UPU 2026 年 9 月公约手册 CN 与 CP 全目录、成品规格及无线电字段。
- `src/FormArt/`、`src/FormSlots.tsv`、`src/OriginalForms.cs`：原式图样及填报位置。`tools/PrepareFormArt.py` 从所提供的九份参考 PDF 提取原式。
- `src/ManualDialog.cs`：首次使用引导和内置使用手册。
- `src/PrintStudio.cs`：纸张、偏移、缩放、可打印区域预检、原生打印机驱动输出。
- `src/Ui.cs`、`MainForm.cs`、`Actions.cs`：玻璃质感 WinForms 界面和业务交互。
- `installer/`：为当前账户安装、升级并建立快捷方式。覆盖安装保留资料，卸载清除默认及当前自定义目录中的程序资料。

资料库是本地单机 XML，不是共享盘多人并发系统。每次提交先在候选副本上验证全部关系，再原子替换主文件。历史 SHA-256 链可检查误改，不宣称数字签名或法证防篡改能力。

## 测试

业务与文档边界检查在 `tests/DomainTests.cs`。`tests/UiTests.cs` 通过实际窗体按钮和对话框验证首次设置、登记、建袋、勾选装袋与打印预览。`tests/PrintDriverTest.cs` **只**选择 Microsoft Print to PDF，不使用实体默认打印机。`tests/InstallerTests.ps1` 在隔离目录验证安装、覆盖升级、卸载清除资料与重装后的空台账。

`--qa "输出目录" --data "独立测试资料目录"` 生成测试用样张和界面截图。`tools/ReviewArtifacts.py` 用于核对导出的原尺寸与条码、编制样张目录。开发测试依赖 PyMuPDF、pypdf、Pillow、NumPy 和 zxing-cpp。

国际邮政单据版式来源为万国邮政联盟（UPU）《公约手册》2026 年 9 月版，内置单据按无线电 QSL 卡片业务改编。国内版式来自所提供的邮政表单样张。 [UPU《公约手册》](https://www.upu.int/UPU/media/upu/files/aboutUpu/acts/06-manualsInThreeVolumes/actInThreeVolumesConventionManual202609En.pdf) · [单据对应关系](docs/单据对应关系.md)
