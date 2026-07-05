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

按 `O` 打开 Windows 文件选择框，手动选择图片。按 `V` 打开视频，视频格式先以 MP4/H.264 为主。图片和视频默认按原始尺寸 pixel-to-pixel 显示，不缩放。如果媒体尺寸刚好是 `15360 x 7680`，在目标 8 屏水平拼接环境中会按原始像素铺满 8 个屏幕。

## 快捷键

- `Esc` 或 `Q`：退出程序。
- `O`：打开图片文件选择框。
- `V`：打开视频文件选择框，优先用于 MP4/H.264。
- `Space`：视频播放/暂停。
- `I`：显示/隐藏屏幕编号、分辨率和坐标信息。
- `S`：图片/视频模式下切换原始尺寸显示和拉伸铺满虚拟桌面显示，默认是原始尺寸。
- `P`：切换测试 pattern 类型。
- `F`：切换窗口置顶状态。
- `R`：重新检测屏幕并重新设置窗口 Bounds。
- `H`：显示/隐藏帮助信息。
- `↑` / `↓`：图片模式下按当前图片所在文件夹排序切换上一张/下一张图片；测试 pattern 模式下切换上一个/下一个 pattern。
- `←` / `→`：视频模式下后退/前进 10 秒。

## 鼠标操作

- 左键单击：图片模式下切换下一张图片；测试 pattern 模式下切换下一个 pattern；视频模式下播放/暂停。
- 左键双击：打开图片或视频文件选择框。
- 右键：打开操作菜单，可打开图片、打开视频、播放/暂停、切换 pattern、切换拉伸/信息/帮助/置顶、重新检测屏幕和退出。
- 滚轮向下：图片模式下切换下一张图片；测试 pattern 模式下切换下一个 pattern。
- 滚轮向上：图片模式下切换上一张图片；测试 pattern 模式下切换上一个 pattern。
- 左键拖动帮助或信息窗口：移动该窗口，避免遮挡测试画面。
- 视频模式下按 `H` 显示帮助时，底部会显示进度条；拖动进度条可跳转播放位置。
- 视频模式下如果焦点落在视频或浮层上，键盘快捷键仍会统一生效。

## 8 屏推荐设置

- Windows 显示模式设置为“扩展这些显示器”。
- 8 个屏幕在 Windows 显示设置里水平排列。
- 每个屏幕分辨率建议一致：`1920 x 7680`。
- 8 个屏幕刷新率、方向、颜色格式建议保持一致。
- 为了严格 pixel-to-pixel，Windows 每个屏幕的缩放比例建议全部设置为 `100%`。
- 如果程序左上角显示的虚拟桌面不是 `15360 x 7680`，请优先检查 Windows 显示器排列、方向、缩放和分辨率。

## 测试 Pattern 内容

测试画面包含：

- 首页诊断页：简洁黑底诊断图，包含每个屏幕区域的绿色边框、每屏左上角的 `Screen 1` 到 `Screen 8`、分辨率、Windows 虚拟桌面坐标、低密度全局坐标网格、中心线、屏幕分割线、1 pixel 横线/竖线测试区域、RGB/白/黑色块、小棋盘格区域。
- 十字框+对角线：黑底、贴虚拟桌面边缘的白色外框、白色中心十字线和两条白色对角线，用于检查几何对齐。
- 水平渐变：画面按高度均分为 4 行，分别显示灰阶、红、绿、蓝横向渐变。
- 垂直渐变：画面按宽度均分为 4 列，分别显示灰阶、红、绿、蓝纵向渐变。
- Colorbar：全屏均分 RGB、CMY、白、黑彩条，用于检查颜色通道。
- 棋盘格：全屏黑白棋盘格，用于检查缩放、错位和 pixel-to-pixel 显示。

绿色边框、屏幕编号、坐标和网格线只在首页诊断页显示。

这些内容用于检查拼接顺序、缩放、错位、颜色通道和 pixel-to-pixel 显示是否正常。

## 常见问题排查

### 只能覆盖一个屏幕

确认 Windows 显示模式是“扩展这些显示器”，不是复制屏幕。普通最大化窗口通常只受主屏工作区限制，本程序会手动设置窗口 `Bounds = virtual desktop bounds`，因此如果仍然只覆盖单屏，优先检查系统是否实际只向 WinForms 暴露了一个 `Screen`。

### 虚拟桌面尺寸不是 15360 x 7680

按 `H` 查看帮助信息，或按 `I` 显示每个屏幕的坐标。检查 Windows 显示设置中 8 个屏幕是否水平排列、每屏是否都是 `1920 x 7680`、是否有屏幕上下错位或方向不同。

### 图片显示没有铺满

按 `O` 选择图片或按 `V` 选择视频后，默认模式是原始尺寸 pixel-to-pixel，不会缩放。只有媒体尺寸正好等于虚拟桌面尺寸时才会完整铺满。按 `S` 可以切换为拉伸铺满，但拉伸会影响 pixel-to-pixel 测试。

### 视频无法播放

视频播放使用 Windows/WPF 媒体管线，优先面向 MP4/H.264。若某个 MP4 打不开，通常是系统缺少对应解码器、文件编码不是 H.264，或封装格式不被当前 Windows 解码链路识别。可先换一个标准 H.264/AAC 的 `.mp4` 测试。

### 视频画面没有铺满

视频默认和图片一样按原始尺寸 pixel-to-pixel 显示，左上角对齐；如果视频小于虚拟桌面，周围黑边是正常现象。按 `S` 可切换为拉伸铺满完整虚拟桌面。视频播放走 Windows/WPF 媒体管线，程序会把视频自然像素和虚拟桌面物理像素按 WPF 当前 DPI scale 转换成 DIP，避免 Windows 缩放不是 100% 时把视频再次放大后只裁出左上角。

### 视频模式下看不到帮助或信息

当前版本的视频帮助、信息和进度条使用真实 WinForms 浮层控件，显示在视频宿主上方。按 `H` 显示帮助，按 `I` 显示屏幕信息；两个浮层都可以用鼠标左键拖动。

### 坐标或像素不一致

程序启动时调用 `Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)`，项目设置 `ApplicationHighDpiMode=PerMonitorV2`，窗体设置 `AutoScaleMode = None`。视频模式下按 `H` 或 `I` 可以查看当前视频 DPI scale；原始视频尺寸会按该 scale 换算后显示为物理像素尺寸。为了跨屏严格像素对应，仍建议所有屏幕 Windows 缩放比例都设为 `100%`；如果多屏缩放比例不一致，Windows/WPF 的混合 DPI 行为仍可能影响跨屏逐像素一致性。

### 图片文件被占用

程序读取图片时会先复制到内存再创建 `Bitmap`，不会长期锁住原图片文件。

Author: `liaoweipeng`
