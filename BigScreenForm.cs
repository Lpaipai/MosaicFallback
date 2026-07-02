using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace MosaicFallback;

public sealed class BigScreenForm : Form
{
    private readonly PatternRenderer _patternRenderer = new();
    private readonly ContextMenuStrip _contextMenu = new();
    private ToolStripMenuItem? _infoMenuItem;
    private ToolStripMenuItem? _stretchMenuItem;
    private ToolStripMenuItem? _topMostMenuItem;
    private ToolStripMenuItem? _helpMenuItem;
    private string? _imagePath;
    private ScreenLayoutInfo _layout = ScreenLayoutInfo.Detect();
    private Image? _image;
    private bool _showInfo = true;
    private bool _stretchImage;
    private bool _showHelp = true;
    private bool _topMost = true;

    public BigScreenForm()
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        KeyPreview = true;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        Text = "Mosaic Fallback Fullscreen";
        TopMost = _topMost;
        ShowInTaskbar = true;
        BuildContextMenu();
        ContextMenuStrip = _contextMenu;

        Load += (_, _) =>
        {
            ApplyVirtualDesktopBounds();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image?.Dispose();
            _contextMenu.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.KeyCode)
        {
            case Keys.Escape:
            case Keys.Q:
                Close();
                break;
            case Keys.I:
                ToggleInfo();
                break;
            case Keys.S:
                ToggleStretch();
                break;
            case Keys.P:
                NextPattern();
                break;
            case Keys.O:
                OpenImageWithDialog();
                break;
            case Keys.F:
                ToggleTopMost();
                break;
            case Keys.R:
                RedetectScreens();
                break;
            case Keys.H:
                ToggleHelp();
                break;
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (e.Button == MouseButtons.Left)
        {
            OpenImageWithDialog();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (e.Delta > 0)
        {
            PreviousPattern();
        }
        else if (e.Delta < 0)
        {
            NextPattern();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.PageUnit = GraphicsUnit.Pixel;
        g.ResetTransform();

        // Client coordinates start at 0,0; translate so virtual desktop
        // screen bounds can be drawn directly even when Windows uses negative coordinates.
        g.TranslateTransform(-_layout.VirtualBounds.Left, -_layout.VirtualBounds.Top);

        if (_image is not null)
        {
            DrawImage(g);
        }
        else
        {
            _patternRenderer.Draw(g, _layout, _layout.VirtualBounds, _showInfo);
        }

        if (_showHelp)
        {
            _patternRenderer.DrawOverlay(g, _layout, BuildHelpText(), new Point(_layout.VirtualBounds.Left + 24, _layout.VirtualBounds.Top + 230));
        }
        else if (_showInfo)
        {
            _patternRenderer.DrawOverlay(g, _layout, _layout.ToDebugText(), new Point(_layout.VirtualBounds.Left + 24, _layout.VirtualBounds.Top + 230));
        }
    }

    private void ApplyVirtualDesktopBounds()
    {
        Rectangle bounds = _layout.VirtualBounds;

        // Normal maximized windows are constrained by the primary monitor/work area.
        // Mosaic-style coverage needs the exact union of all extended desktop screens,
        // including negative X/Y coordinates, so the bounds are set manually.
        Bounds = new Rectangle(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        WindowState = FormWindowState.Normal;
        TopMost = _topMost;
    }

    private void BuildContextMenu()
    {
        _contextMenu.Items.Add("打开图片(&O)", null, (_, _) => OpenImageWithDialog());
        _contextMenu.Items.Add("上一个 Pattern", null, (_, _) => PreviousPattern());
        _contextMenu.Items.Add("下一个 Pattern(&P)", null, (_, _) => NextPattern());
        _contextMenu.Items.Add(new ToolStripSeparator());

        _stretchMenuItem = new ToolStripMenuItem("拉伸铺满(&S)", null, (_, _) => ToggleStretch());
        _infoMenuItem = new ToolStripMenuItem("显示信息(&I)", null, (_, _) => ToggleInfo());
        _helpMenuItem = new ToolStripMenuItem("显示帮助(&H)", null, (_, _) => ToggleHelp());
        _topMostMenuItem = new ToolStripMenuItem("窗口置顶(&F)", null, (_, _) => ToggleTopMost());
        _contextMenu.Items.Add(_stretchMenuItem);
        _contextMenu.Items.Add(_infoMenuItem);
        _contextMenu.Items.Add(_helpMenuItem);
        _contextMenu.Items.Add(_topMostMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        _contextMenu.Items.Add("重新检测屏幕(&R)", null, (_, _) => RedetectScreens());
        _contextMenu.Items.Add("退出", null, (_, _) => Close());
        _contextMenu.Opening += (_, _) => UpdateContextMenuState();
    }

    private void UpdateContextMenuState()
    {
        if (_stretchMenuItem is not null)
        {
            _stretchMenuItem.Checked = _stretchImage;
            _stretchMenuItem.Enabled = _image is not null;
        }

        if (_infoMenuItem is not null)
        {
            _infoMenuItem.Checked = _showInfo;
        }

        if (_helpMenuItem is not null)
        {
            _helpMenuItem.Checked = _showHelp;
        }

        if (_topMostMenuItem is not null)
        {
            _topMostMenuItem.Checked = _topMost;
        }
    }

    private void NextPattern()
    {
        _patternRenderer.NextPattern();
        ClearImage();
        Invalidate();
    }

    private void PreviousPattern()
    {
        _patternRenderer.PreviousPattern();
        ClearImage();
        Invalidate();
    }

    private void ClearImage()
    {
        _image?.Dispose();
        _image = null;
        _imagePath = null;
    }

    private void ToggleInfo()
    {
        _showInfo = !_showInfo;
        Invalidate();
    }

    private void ToggleStretch()
    {
        _stretchImage = !_stretchImage;
        Invalidate();
    }

    private void ToggleTopMost()
    {
        _topMost = !_topMost;
        TopMost = _topMost;
        Invalidate();
    }

    private void RedetectScreens()
    {
        _layout = ScreenLayoutInfo.Detect();
        ApplyVirtualDesktopBounds();
        Invalidate();
    }

    private void ToggleHelp()
    {
        _showHelp = !_showHelp;
        Invalidate();
    }

    private void OpenImageWithDialog()
    {
        bool restoreTopMost = TopMost;
        TopMost = false;

        using OpenFileDialog dialog = new()
        {
            Title = "打开测试图片",
            Filter = "图片文件|*.bmp;*.gif;*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = false,
            RestoreDirectory = true
        };

        DialogResult result = dialog.ShowDialog(this);
        TopMost = restoreTopMost;

        if (result != DialogResult.OK)
        {
            return;
        }

        LoadImage(dialog.FileName);
    }

    private void LoadImage(string path)
    {
        string fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            MessageBox.Show($"图片不存在：{fullPath}", "MosaicFallback", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using MemoryStream memory = new();
            stream.CopyTo(memory);
            memory.Position = 0;

            using Image loaded = Image.FromStream(memory);
            Bitmap bitmap = new(loaded);

            _image?.Dispose();
            _image = bitmap;
            _imagePath = fullPath;
            _stretchImage = false;
            Invalidate();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"图片加载失败：{ex.Message}", "MosaicFallback", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void DrawImage(Graphics g)
    {
        if (_image is null)
        {
            return;
        }

        g.InterpolationMode = _stretchImage ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.Clear(Color.Black);

        if (_stretchImage)
        {
            g.DrawImage(_image, _layout.VirtualBounds);
            return;
        }

        Rectangle destination = new(_layout.VirtualBounds.Left, _layout.VirtualBounds.Top, _image.Width, _image.Height);
        g.DrawImageUnscaled(_image, destination.Location);
    }

    private string BuildHelpText()
    {
        StringBuilder builder = new();
        builder.AppendLine("快捷键：Esc/Q 退出 | O 打开图片 | I 信息 | S 原始/拉伸 | P Pattern | F 置顶 | R 重检屏幕 | H 帮助");
        builder.AppendLine("鼠标：左键双击打开图片 | 右键菜单 | 滚轮切换 Pattern");
        builder.AppendLine($"虚拟桌面：X={_layout.VirtualBounds.Left}, Y={_layout.VirtualBounds.Top}, W={_layout.VirtualBounds.Width}, H={_layout.VirtualBounds.Height}");
        builder.AppendLine($"模式：{(_image is null ? "测试 pattern" : "图片显示")} ({_patternRenderer.PatternName})");
        builder.AppendLine($"图片：{(_imagePath ?? "未选择")}");
        if (_image is not null)
        {
            builder.AppendLine($"图片尺寸：{_image.Width} x {_image.Height}");
        }
        builder.AppendLine($"显示：{(_stretchImage ? "拉伸铺满虚拟桌面" : "原始尺寸 pixel-to-pixel")}");
        builder.AppendLine($"置顶：{(_topMost ? "是" : "否")}");
        builder.AppendLine(_layout.ToDebugText());
        builder.AppendLine("Author: liaoweipeng");
        return builder.ToString().TrimEnd();
    }
}
