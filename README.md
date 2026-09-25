# Local QR Scanner

Local QR Scanner 是一款面向 [PowerToys Run](https://learn.microsoft.com/windows/powertoys/run) 的离线二维码识别插件。它可以让你在屏幕上拖动框选截图区域，只识别选区内的二维码，并提供内容预览与独立的扫描历史主界面。

识别过程不调用远程 API，也不会上传屏幕截图。网页链接只会显示地址；只有在用户明确选择“打开”时，才会交给系统默认应用处理。

## 功能特性

- **区域截图识别**：按下 `Alt + Space` 并输入 `qr`，拖动框选二维码所在区域，只识别选中的截图。
- **结果立即呈现**：识别结束后自动弹出本次结果页面，无需再打开历史记录查找。
- **纯本地识别**：基于 ZXing.Net 在本机完成二维码解码，不需要账号、API Key 或网络连接。
- **小二维码优化**：较大的选区会自动分块复扫，提高网页和桌面应用中小尺寸二维码的识别率。
- **多种内容预览**：支持文本、网页链接、Wi-Fi 配置、联系人、内嵌图片和本地图片。
- **隐私友好的图片处理**：可选保存二维码附近的局部截图，从不保存完整屏幕。
- **扫描历史主界面**：历史记录不混入 `qr` 搜索结果，在独立面板中按时间查看、搜索、复制、预览或删除。
- **自动清理**：可在 PowerToys 设置中配置历史保留天数、最大记录数以及是否保存局部截图。
- **剪贴板识别**：可以识别已经复制到剪贴板的截图或图片。

## 系统要求

- Windows 10 2004 或更高版本
- PowerToys 0.97 或更高版本，并启用 PowerToys Run
- x64 或 ARM64 处理器

普通安装不需要 .NET SDK；只有从源码构建时才需要 .NET 9 SDK。

## 安装

### 使用安装程序

1. 从系统托盘退出 PowerToys。
2. 运行 `LocalQrScanner-Setup-0.1.1-x64.exe`。
3. 安装完成后重新启动 PowerToys。
4. 按 `Alt + Space`，输入 `qr` 验证插件。

安装程序会将插件安装到当前用户的标准目录，不需要管理员权限：

```text
%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\LocalQrScanner
```

### 手动安装

1. 下载与你的处理器架构一致的 ZIP 包并解压。
2. 退出 PowerToys。
3. 将包内全部文件复制到上述 `LocalQrScanner` 目录。
4. 重新启动 PowerToys。

> 当前安装程序没有商业代码签名，Windows SmartScreen 可能显示“未知发布者”。建议从项目 Release 页面下载并核对 SHA-256 摘要。

## 使用方法

按 `Alt + Space` 打开 PowerToys Run，然后使用以下命令：

| 命令 | 功能 |
| --- | --- |
| `qr` | 显示区域截图、剪贴板识别、历史记录面板和设置入口 |
| `qr scan` | 框选屏幕区域并只识别选区内的二维码 |
| `qr clipboard` | 识别剪贴板图片中的二维码 |
| `qr history` | 打开独立的历史记录主界面 |
| `qr history 关键词` | 打开历史主界面并搜索指定内容 |
| `qr settings` | 打开 PowerToys Run 插件设置 |
| `qr clear` | 清空历史和已保存的二维码局部截图 |

截图识别启动后，PowerToys Run 会先关闭，然后显示当前桌面的冻结画面。按住鼠标左键拖动框选二维码，松开后仅识别该选区；按 `Esc` 或鼠标右键可以取消。识别完成后会立即弹出“本次识别结果”页面，可直接查看和复制内容；结果同时自动保存到历史记录。未识别到二维码或发生错误时也会直接弹窗说明。

触发词 `qr` 可以在 PowerToys Run 的插件设置中修改。

历史记录面板支持以下操作：

- 搜索二维码内容、类型或扫描来源；
- 查看保存的二维码局部截图和完整内容；
- 复制内容、打开完整预览或删除单条记录；
- 刷新或清空全部历史。

## 内容预览

| 内容类型 | 预览行为 |
| --- | --- |
| 普通文本、Wi-Fi、联系人 | 在只读文本框中本地显示 |
| `data:image/...;base64,...` | 在内存中解码并显示图片 |
| 本地图片路径或 `file://` 地址 | 读取本地文件并显示 |
| HTTP/HTTPS 链接 | 显示并允许复制，不后台访问 |
| 远程图片链接 | 仅显示地址，不下载图片 |

PowerToys Run 本身没有第三方插件可用的富媒体预览面板，因此图片和完整内容会在插件自己的轻量 WPF 窗口中显示。

## 设置与本地数据

在 **PowerToys 设置 → PowerToys Run → 插件 → Local QR Scanner** 中可以配置：

- 历史保留天数，默认 30 天；
- 最大历史记录数，默认 200 条；
- 是否保存二维码局部截图，默认开启。

运行数据保存在：

```text
%LOCALAPPDATA%\LocalQrScanner
├── history.json
└── captures\
```

卸载插件时会保留历史数据，避免意外丢失。若不再需要，可手动删除该目录或先执行 `qr clear`。

## 隐私与安全

- 插件运行时代码不包含 HTTP 客户端、遥测或更新检查。
- 桌面冻结画面和选区位图只在内存中用于截图与二维码识别，操作结束后立即释放。
- ZXing 只接收用户实际框选的区域，不会识别选区以外的屏幕内容。
- 开启截图保存时，仅保存识别到的二维码附近区域。
- 内嵌图片和本地图片在本机解码；远程内容不会被插件下载。
- 打开链接是显式用户操作，目标由 Windows 默认应用处理。

## 从源码构建

### 开发环境

- .NET 9 SDK
- PowerShell 7 或 Windows PowerShell 5.1
- Inno Setup 6/7，仅在构建图形化安装程序时需要

插件保持使用 PowerToys 的宿主加载上下文，以兼容宿主自带的接口程序集版本。插件只为自己的 `zxing.dll` 注册白名单本地解析器；构建脚本会自动检查清单设置和依赖文件，避免将来再次漏装或无法解析。

构建并运行测试：

```powershell
dotnet test .\LocalQrScanner.slnx -c Release -p:Platform=x64
```

构建插件：

```powershell
.\scripts\build.ps1 -Platform x64
```

创建 ZIP 包：

```powershell
.\scripts\pack.ps1 -Platform x64
```

创建 Windows 安装程序：

```powershell
winget install --id JRSoftware.InnoSetup -e
.\scripts\build-installer.ps1
```

产物会写入 `artifacts` 目录。开发机也可以在退出 PowerToys 后直接安装构建结果：

```powershell
.\scripts\install.ps1 -Platform x64
```

## 技术架构

```text
PowerToys Run (`qr`)
        │
        ├── 鼠标框选截图 ── 选区识别 + 大图重叠分块识别
        ├── 剪贴板图片 ──── 本地 ZXing.Net 解码
        ├── 历史记录入口 ── 独立历史主界面
        └── 设置入口 ────── PowerToys Run 插件设置
                                │
                                ▼
                         内容类型检测
                                │
             ┌──────────────────┼──────────────────┐
             ▼                  ▼                  ▼
          本地预览          history.json       局部二维码截图
```

项目目录：

```text
Community.PowerToys.Run.Plugin.LocalQrScanner/           插件、扫描器与预览窗口
Community.PowerToys.Run.Plugin.LocalQrScanner.UnitTests/ 自动化测试
installer/                                                Inno Setup 安装器
scripts/                                                  构建、打包与安装脚本
```

## 参与贡献

欢迎提交 Issue 或 Pull Request。提交代码前请确保 Release 构建无警告，并且所有自动化测试通过。涉及新内容格式时，请同时补充分类或识别测试。

## 许可证

本项目使用 [MIT License](LICENSE)。第三方组件及其许可证见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
# LocalQrScanner
