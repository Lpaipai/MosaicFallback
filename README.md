# MosaicFallback

Windows 扩展桌面下的 Mosaic 替代全屏显示工具。程序不依赖 NVIDIA Mosaic，也不调用显卡专有 API，只基于 Windows `Screen.AllScreens` 检测当前所有显示器，计算虚拟桌面 Union Bounds，并创建一个无边框、置顶、无标题栏窗口覆盖完整虚拟桌面。

目标环境是 8 个水平排列的扩展屏，每屏 `1920 x 7680`，总虚拟桌面 `15360 x 7680`。程序不会假设虚拟桌面左上角是 `0,0`，显示器存在负坐标时也会把窗口放到虚拟桌面的 `Left, Top`，并把窗口尺寸设置为 `Width, Height`。

## 编译方式

本项目使用 .NET WinForms：

```powershell
dotnet publish .\MosaicFallback.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

输出位置：

```text
bin\Release\net7.0-windows\win-x64\publish\MosaicFallback.exe
```

自包含便携目录版发布：

```powershell
dotnet publish .\MosaicFallback.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishDir=bin\Release\self-contained\
```

本次已生成可直接运行的便携目录版入口：

```text
dist\MosaicFallback.exe
```

`dist` 目录内的 DLL 是自包含运行所需文件，请保留在 `MosaicFallback.exe` 同目录。这样双击 exe 时不依赖 .NET bundle 解压缓存，也不要求目标机预装 .NET runtime。

如果机器没有 .NET SDK，需要先安装或解压 .NET SDK。框架依赖版 exe 需要 Windows Desktop Runtime 7.0；自包含便携目录版不要求目标机预装 .NET runtime。

## 运行方式

双击或命令行启动后先显示测试 pattern：

```powershell
.\MosaicFallback.exe
```

按 `O` 打开 Windows 文件选择框，手动选择图片。图片默认按原始尺寸 pixel-to-pixel 显示，不缩放。如果图片刚好是 `15360 x 7680`，在目标 8 屏水平拼接环境中会按原始像素铺满 8 个屏幕。

## 快捷键

- `Esc` 或 `Q`：退出程序。
- `O`：打开图片文件选择框。
- `I`：显示/隐藏屏幕编号、分辨率和坐标信息。
- `S`：图片模式下切换原始尺寸显示和拉伸铺满虚拟桌面显示，默认是原始尺寸。
- `P`：切换测试 pattern 类型。
- `F`：切换窗口置顶状态。
- `R`：重新检测屏幕并重新设置窗口 Bounds。
- `H`：显示/隐藏帮助信息。

## 鼠标操作

- 左键双击：打开图片文件选择框。
- 右键：打开操作菜单，可打开图片、切换 pattern、切换拉伸/信息/帮助/置顶、重新检测屏幕和退出。
- 滚轮向下：下一个 pattern。
- 滚轮向上：上一个 pattern。

## 8 屏推荐设置

- Windows 显示模式设置为“扩展这些显示器”。
- 8 个屏幕在 Windows 显示设置里水平排列。
- 每个屏幕分辨率建议一致：`1920 x 7680`。
- 8 个屏幕刷新率、方向、颜色格式建议保持一致。
- 为了严格 pixel-to-pixel，Windows 每个屏幕的缩放比例建议全部设置为 `100%`。
- 如果程序左上角显示的虚拟桌面不是 `15360 x 7680`，请优先检查 Windows 显示器排列、方向、缩放和分辨率。

## 测试 Pattern 内容

测试画面包含：

- 首页诊断页：每个屏幕区域的绿色边框、每屏左上角的 `Screen 1` 到 `Screen 8`、分辨率、Windows 虚拟桌面坐标、全局坐标网格、中心线、屏幕分割线、1 pixel 横线/竖线测试区域、RGB 单色块、棋盘格区域。
- 十字框+对角线：黑底、贴虚拟桌面边缘的白色外框、白色中心十字线和两条白色对角线，用于检查几何对齐。
- 水平渐变：画面按高度均分为 4 行，分别显示灰阶、红、绿、蓝横向渐变。
- 垂直渐变：画面按宽度均分为 4 列，分别显示灰阶、红、绿、蓝纵向渐变。
- Colorbar：全屏均分 RGB、CMY、白、黑彩条，用于检查颜色通道。

绿色边框、屏幕编号、坐标和网格线只在首页诊断页显示。

这些内容用于检查拼接顺序、缩放、错位、颜色通道和 pixel-to-pixel 显示是否正常。

## 常见问题排查

### 只能覆盖一个屏幕

确认 Windows 显示模式是“扩展这些显示器”，不是复制屏幕。普通最大化窗口通常只受主屏工作区限制，本程序会手动设置窗口 `Bounds = virtual desktop bounds`，因此如果仍然只覆盖单屏，优先检查系统是否实际只向 WinForms 暴露了一个 `Screen`。

### 虚拟桌面尺寸不是 15360 x 7680

按 `H` 查看帮助信息，或按 `I` 显示每个屏幕的坐标。检查 Windows 显示设置中 8 个屏幕是否水平排列、每屏是否都是 `1920 x 7680`、是否有屏幕上下错位或方向不同。

### 图片显示没有铺满

按 `O` 选择图片后，默认模式是原始尺寸 pixel-to-pixel，不会缩放。只有图片尺寸正好等于虚拟桌面尺寸时才会完整铺满。按 `S` 可以切换为拉伸铺满，但拉伸会影响 pixel-to-pixel 测试。

### 坐标或像素不一致

程序启动时调用 `Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)`，项目设置 `ApplicationHighDpiMode=PerMonitorV2`，窗体设置 `AutoScaleMode = None`。为了严格像素对应，建议所有屏幕 Windows 缩放比例都设为 `100%`。

### 图片文件被占用

程序读取图片时会先复制到内存再创建 `Bitmap`，不会长期锁住原图片文件。

Author: `liaoweipeng`
