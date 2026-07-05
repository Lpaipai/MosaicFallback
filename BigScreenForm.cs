using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace MosaicFallback;

public sealed class BigScreenForm : Form, IMessageFilter
{
    private static readonly string[] ImageExtensions = { ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };
    private static readonly string[] VideoExtensions = { ".mp4", ".m4v", ".mov" };
    private static readonly TimeSpan VideoSeekStep = TimeSpan.FromSeconds(10);

    private enum OverlayPanel
    {
        None,
        Help,
        Info
    }

    private readonly PatternRenderer _patternRenderer = new();
    private readonly WpfVideoPlayer _videoPlayer;
    private readonly ContextMenuStrip _contextMenu = new();
    private readonly System.Windows.Forms.Timer _singleClickTimer = new();
    private readonly System.Windows.Forms.Timer _videoUiTimer = new();
    private readonly TextBox _helpOverlayControl = new();
    private readonly TextBox _infoOverlayControl = new();
    private readonly Panel _progressPanel = new();
    private readonly Panel _progressFill = new();
    private readonly Label _progressLabel = new();
    private ToolStripMenuItem? _infoMenuItem;
    private ToolStripMenuItem? _stretchMenuItem;
    private ToolStripMenuItem? _topMostMenuItem;
    private ToolStripMenuItem? _helpMenuItem;
    private string? _imagePath;
    private string? _videoPath;
    private string[] _imagePlaylist = Array.Empty<string>();
    private int _imagePlaylistIndex = -1;
    private ScreenLayoutInfo _layout = ScreenLayoutInfo.Detect();
    private Image? _image;
    private bool _showInfo = true;
    private bool _stretchImage;
    private bool _showHelp = true;
    private bool _topMost = true;
    private Point? _helpOverlayLocation;
    private Point? _infoOverlayLocation;
    private Rectangle _helpOverlayBounds = Rectangle.Empty;
    private Rectangle _infoOverlayBounds = Rectangle.Empty;
    private OverlayPanel _draggingOverlay = OverlayPanel.None;
    private Point _dragOffset;
    private bool _dragMoved;
    private bool _progressDragging;

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
        Application.AddMessageFilter(this);
        _videoPlayer = new WpfVideoPlayer(this);
        _videoPlayer.MediaOpened += (_, _) =>
        {
            UpdateVideoDestination();
            UpdateOverlayControls();
            Invalidate();
        };
        _videoPlayer.PlaybackFailed += (_, message) =>
        {
            MessageBox.Show($"视频播放失败：{message}", "MosaicFallback", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        BuildContextMenu();
        BuildOverlayControls();
        ContextMenuStrip = _contextMenu;
        _videoPlayer.HostControl.ContextMenuStrip = _contextMenu;
        _videoPlayer.HostControl.MouseClick += (_, e) => HandleMouseClick(e);
        _videoPlayer.HostControl.MouseDoubleClick += (_, e) => HandleMouseDoubleClick(e);
        _videoPlayer.HostControl.MouseWheel += (_, e) => HandleMouseWheel(e);
        _videoPlayer.HostControl.KeyDown += (_, e) => HandleShortcutKey(e);
        _singleClickTimer.Interval = SystemInformation.DoubleClickTime + 40;
        _singleClickTimer.Tick += (_, _) =>
        {
            _singleClickTimer.Stop();
            HandleSingleClick();
        };
        _videoUiTimer.Interval = 250;
        _videoUiTimer.Tick += (_, _) =>
        {
            if (!_progressDragging)
            {
                UpdateOverlayControls();
            }
        };

        Load += (_, _) =>
        {
            ApplyVirtualDesktopBounds();
            UpdateOverlayControls();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image?.Dispose();
            _videoPlayer.Dispose();
            _contextMenu.Dispose();
            _singleClickTimer.Dispose();
            _videoUiTimer.Dispose();
            Application.RemoveMessageFilter(this);
        }

        base.Dispose(disposing);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        HandleShortcutKey(e);
    }

    public bool PreFilterMessage(ref Message m)
    {
        const int wmKeyDown = 0x0100;
        const int wmSysKeyDown = 0x0104;

        if (m.Msg is not wmKeyDown and not wmSysKeyDown)
        {
            return false;
        }

        if (!ContainsFocus && Form.ActiveForm != this)
        {
            return false;
        }

        Keys keyCode = (Keys)m.WParam.ToInt32() & Keys.KeyCode;
        return HandleShortcutKey(keyCode);
    }

    private void HandleShortcutKey(KeyEventArgs e)
    {
        if (HandleShortcutKey(e.KeyCode))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private bool HandleShortcutKey(Keys keyCode)
    {
        bool hasImage = _image is not null;
        bool hasVideo = _videoPath is not null;

        switch (keyCode)
        {
            case Keys.Escape:
            case Keys.Q:
                Close();
                return true;
            case Keys.I:
                ToggleInfo();
                return true;
            case Keys.S:
                ToggleStretch();
                return true;
            case Keys.Space:
                ToggleVideoPlayback();
                return true;
            case Keys.P:
                NextPattern();
                return true;
            case Keys.O:
                OpenImageWithDialog();
                return true;
            case Keys.V:
                OpenVideoWithDialog();
                return true;
            case Keys.F:
                ToggleTopMost();
                return true;
            case Keys.R:
                RedetectScreens();
                return true;
            case Keys.H:
                ToggleHelp();
                return true;
            case Keys.Up:
                if (hasImage)
                {
                    SwitchImage(-1);
                    return true;
                }

                if (!hasVideo)
                {
                    PreviousPattern();
                    return true;
                }

                return true;
            case Keys.Down:
                if (hasImage)
                {
                    SwitchImage(1);
                    return true;
                }

                if (!hasVideo)
                {
                    NextPattern();
                    return true;
                }

                return true;
            case Keys.Left:
                SeekVideoBy(-VideoSeekStep);
                return _videoPath is not null;
            case Keys.Right:
                SeekVideoBy(VideoSeekStep);
                return _videoPath is not null;
            default:
                return false;
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (e.Button == MouseButtons.Left)
        {
            HandleMouseDoubleClick(e);
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        HandleMouseClick(e);
    }

    private void HandleMouseClick(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_dragMoved)
        {
            _dragMoved = false;
            return;
        }

        Point virtualPoint = ClientToVirtualPoint(e.Location);
        if (_helpOverlayBounds.Contains(virtualPoint) || _infoOverlayBounds.Contains(virtualPoint))
        {
            return;
        }

        _singleClickTimer.Stop();
        _singleClickTimer.Start();
    }

    private void HandleMouseDoubleClick(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _singleClickTimer.Stop();
        Point virtualPoint = ClientToVirtualPoint(e.Location);
        if (_helpOverlayBounds.Contains(virtualPoint) || _infoOverlayBounds.Contains(virtualPoint))
        {
            return;
        }

        OpenMediaWithDialog();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        HandleMouseWheel(e);
    }

    private void HandleMouseWheel(MouseEventArgs e)
    {
        if (e.Delta > 0)
        {
            if (_image is not null)
            {
                SwitchImage(-1);
            }
            else if (_videoPath is null)
            {
                PreviousPattern();
            }
        }
        else if (e.Delta < 0)
        {
            if (_image is not null)
            {
                SwitchImage(1);
            }
            else if (_videoPath is null)
            {
                NextPattern();
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragMoved = false;
        Point virtualPoint = ClientToVirtualPoint(e.Location);
        if (_helpOverlayBounds.Contains(virtualPoint))
        {
            _draggingOverlay = OverlayPanel.Help;
            _dragOffset = new Point(virtualPoint.X - _helpOverlayBounds.Left, virtualPoint.Y - _helpOverlayBounds.Top);
            _dragMoved = false;
            Capture = true;
        }
        else if (_infoOverlayBounds.Contains(virtualPoint))
        {
            _draggingOverlay = OverlayPanel.Info;
            _dragOffset = new Point(virtualPoint.X - _infoOverlayBounds.Left, virtualPoint.Y - _infoOverlayBounds.Top);
            _dragMoved = false;
            Capture = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_draggingOverlay == OverlayPanel.None)
        {
            Point virtualPoint = ClientToVirtualPoint(e.Location);
            Cursor = _helpOverlayBounds.Contains(virtualPoint) || _infoOverlayBounds.Contains(virtualPoint) ? Cursors.SizeAll : Cursors.Default;
            return;
        }

        Point current = ClientToVirtualPoint(e.Location);
        Point next = new(current.X - _dragOffset.X, current.Y - _dragOffset.Y);
        _dragMoved = true;

        if (_draggingOverlay == OverlayPanel.Help)
        {
            _helpOverlayLocation = next;
        }
        else if (_draggingOverlay == OverlayPanel.Info)
        {
            _infoOverlayLocation = next;
        }

        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Left && _draggingOverlay != OverlayPanel.None)
        {
            _draggingOverlay = OverlayPanel.None;
            Capture = false;
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
        else if (_videoPath is not null)
        {
            g.Clear(Color.Black);
        }
        else
        {
            _patternRenderer.Draw(g, _layout, _layout.VirtualBounds, _showInfo);
        }

        if (_showHelp && _videoPath is null)
        {
            _helpOverlayBounds = _patternRenderer.DrawOverlay(g, BuildHelpText(), GetHelpOverlayLocation());
        }
        else if (_videoPath is null)
        {
            _helpOverlayBounds = Rectangle.Empty;
        }

        if (_showInfo && _videoPath is null)
        {
            _infoOverlayBounds = _patternRenderer.DrawOverlay(g, _layout.ToDebugText(), GetInfoOverlayLocation());
        }
        else if (_videoPath is null)
        {
            _infoOverlayBounds = Rectangle.Empty;
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
        ResetOverlayLocations();
        UpdateVideoDestination();
        UpdateOverlayControls();
    }

    private Point ClientToVirtualPoint(Point clientPoint)
    {
        return new Point(clientPoint.X + _layout.VirtualBounds.Left, clientPoint.Y + _layout.VirtualBounds.Top);
    }

    private Point GetHelpOverlayLocation()
    {
        if (_helpOverlayLocation is null)
        {
            _helpOverlayLocation = new Point(_layout.VirtualBounds.Left + 24, _layout.VirtualBounds.Top + 230);
        }

        return _helpOverlayLocation.Value;
    }

    private Point GetInfoOverlayLocation()
    {
        if (_infoOverlayLocation is null)
        {
            _infoOverlayLocation = new Point(_layout.VirtualBounds.Left + 24, _layout.VirtualBounds.Top + 680);
        }

        return _infoOverlayLocation.Value;
    }

    private void ResetOverlayLocations()
    {
        _helpOverlayLocation = new Point(_layout.VirtualBounds.Left + 24, _layout.VirtualBounds.Top + 230);
        _infoOverlayLocation = new Point(_layout.VirtualBounds.Left + 24, _layout.VirtualBounds.Top + 680);
    }

    private void BuildOverlayControls()
    {
        ConfigureOverlayTextBox(_helpOverlayControl);
        ConfigureOverlayTextBox(_infoOverlayControl);

        _helpOverlayControl.MouseDown += (_, e) => BeginOverlayDrag(OverlayPanel.Help, _helpOverlayControl, e);
        _helpOverlayControl.MouseMove += (_, e) => ContinueOverlayDrag(_helpOverlayControl, e);
        _helpOverlayControl.MouseUp += (_, e) => EndOverlayDrag(e);
        _helpOverlayControl.MouseDoubleClick += (_, _) => _singleClickTimer.Stop();
        _helpOverlayControl.MouseWheel += (_, e) => HandleMouseWheel(e);
        _helpOverlayControl.KeyDown += (_, e) => HandleShortcutKey(e);

        _infoOverlayControl.MouseDown += (_, e) => BeginOverlayDrag(OverlayPanel.Info, _infoOverlayControl, e);
        _infoOverlayControl.MouseMove += (_, e) => ContinueOverlayDrag(_infoOverlayControl, e);
        _infoOverlayControl.MouseUp += (_, e) => EndOverlayDrag(e);
        _infoOverlayControl.MouseDoubleClick += (_, _) => _singleClickTimer.Stop();
        _infoOverlayControl.MouseWheel += (_, e) => HandleMouseWheel(e);
        _infoOverlayControl.KeyDown += (_, e) => HandleShortcutKey(e);

        _progressPanel.BackColor = Color.FromArgb(28, 28, 28);
        _progressPanel.BorderStyle = BorderStyle.FixedSingle;
        _progressPanel.Visible = false;
        _progressPanel.TabStop = false;
        _progressPanel.Cursor = Cursors.Hand;
        _progressPanel.MouseDown += ProgressPanel_MouseDown;
        _progressPanel.MouseMove += ProgressPanel_MouseMove;
        _progressPanel.MouseUp += ProgressPanel_MouseUp;
        _progressPanel.MouseDoubleClick += (_, _) => _singleClickTimer.Stop();
        _progressPanel.KeyDown += (_, e) => HandleShortcutKey(e);

        _progressFill.BackColor = Color.Lime;
        _progressPanel.Controls.Add(_progressFill);

        _progressLabel.AutoSize = false;
        _progressLabel.BackColor = Color.Transparent;
        _progressLabel.ForeColor = Color.White;
        _progressLabel.Font = new Font("Consolas", 18, FontStyle.Regular, GraphicsUnit.Pixel);
        _progressLabel.TextAlign = ContentAlignment.MiddleCenter;
        _progressLabel.MouseDown += ProgressPanel_MouseDown;
        _progressLabel.MouseMove += ProgressPanel_MouseMove;
        _progressLabel.MouseUp += ProgressPanel_MouseUp;
        _progressLabel.MouseDoubleClick += (_, _) => _singleClickTimer.Stop();
        _progressLabel.KeyDown += (_, e) => HandleShortcutKey(e);
        _progressPanel.Controls.Add(_progressLabel);

        Controls.Add(_helpOverlayControl);
        Controls.Add(_infoOverlayControl);
        Controls.Add(_progressPanel);
        UpdateOverlayControls();
    }

    private static void ConfigureOverlayTextBox(TextBox textBox)
    {
        textBox.BackColor = Color.Black;
        textBox.ForeColor = Color.White;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Font = new Font("Consolas", 18, FontStyle.Regular, GraphicsUnit.Pixel);
        textBox.Multiline = true;
        textBox.ReadOnly = true;
        textBox.ScrollBars = ScrollBars.None;
        textBox.TabStop = false;
        textBox.WordWrap = false;
        textBox.Visible = false;
        textBox.Cursor = Cursors.SizeAll;
    }

    private void BeginOverlayDrag(OverlayPanel panel, Control control, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _singleClickTimer.Stop();
        _draggingOverlay = panel;
        Point clientPoint = PointToClient(control.PointToScreen(e.Location));
        Point virtualPoint = ClientToVirtualPoint(clientPoint);
        Rectangle bounds = panel == OverlayPanel.Help ? _helpOverlayBounds : _infoOverlayBounds;
        _dragOffset = new Point(virtualPoint.X - bounds.Left, virtualPoint.Y - bounds.Top);
        _dragMoved = false;
        control.Capture = true;
    }

    private void ContinueOverlayDrag(Control control, MouseEventArgs e)
    {
        if (_draggingOverlay == OverlayPanel.None)
        {
            return;
        }

        Point clientPoint = PointToClient(control.PointToScreen(e.Location));
        Point virtualPoint = ClientToVirtualPoint(clientPoint);
        Point next = new(virtualPoint.X - _dragOffset.X, virtualPoint.Y - _dragOffset.Y);
        _dragMoved = true;

        if (_draggingOverlay == OverlayPanel.Help)
        {
            _helpOverlayLocation = next;
        }
        else if (_draggingOverlay == OverlayPanel.Info)
        {
            _infoOverlayLocation = next;
        }

        UpdateOverlayControls();
    }

    private void EndOverlayDrag(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _draggingOverlay = OverlayPanel.None;
        _helpOverlayControl.Capture = false;
        _infoOverlayControl.Capture = false;
        _dragMoved = false;
    }

    private void UpdateOverlayControls()
    {
        bool useControlOverlays = _videoPath is not null;
        if (!useControlOverlays)
        {
            _helpOverlayControl.Visible = false;
            _infoOverlayControl.Visible = false;
            _progressPanel.Visible = false;
            return;
        }

        _videoPlayer.HostControl.BringToFront();

        if (_showHelp)
        {
            ApplyOverlayText(_helpOverlayControl, BuildHelpText(), GetHelpOverlayLocation(), out _helpOverlayBounds);
            _helpOverlayControl.Visible = true;
            _helpOverlayControl.BringToFront();
        }
        else
        {
            _helpOverlayControl.Visible = false;
            _helpOverlayBounds = Rectangle.Empty;
        }

        if (_showInfo)
        {
            ApplyOverlayText(_infoOverlayControl, _layout.ToDebugText(), GetInfoOverlayLocation(), out _infoOverlayBounds);
            _infoOverlayControl.Visible = true;
            _infoOverlayControl.BringToFront();
        }
        else
        {
            _infoOverlayControl.Visible = false;
            _infoOverlayBounds = Rectangle.Empty;
        }

        UpdateProgressBar();
        _progressPanel.BringToFront();
    }

    private void ApplyOverlayText(TextBox textBox, string text, Point virtualLocation, out Rectangle virtualBounds)
    {
        Point clientLocation = VirtualToClientPoint(virtualLocation);
        int maxWidth = Math.Max(320, Math.Min(1220, ClientSize.Width - clientLocation.X - 24));
        Size measured = TextRenderer.MeasureText(
            text,
            textBox.Font,
            new Size(maxWidth, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
        int width = Math.Min(maxWidth, measured.Width + 34);
        int maxHeight = Math.Max(160, ClientSize.Height - clientLocation.Y - 96);
        int height = Math.Min(maxHeight, measured.Height + 30);

        textBox.Text = text;
        textBox.SetBounds(clientLocation.X, clientLocation.Y, width, height);
        virtualBounds = new Rectangle(virtualLocation.X, virtualLocation.Y, width, height);
    }

    private Point VirtualToClientPoint(Point virtualPoint)
    {
        return new Point(virtualPoint.X - _layout.VirtualBounds.Left, virtualPoint.Y - _layout.VirtualBounds.Top);
    }

    private void UpdateProgressBar()
    {
        bool visible = _videoPath is not null && _showHelp;
        _progressPanel.Visible = visible;
        if (!visible)
        {
            return;
        }

        int width = Math.Max(360, ClientSize.Width - 48);
        int height = 42;
        _progressPanel.SetBounds(24, Math.Max(0, ClientSize.Height - height - 24), width, height);
        _progressLabel.SetBounds(0, 0, width, height);

        TimeSpan duration = _videoPlayer.Duration;
        TimeSpan position = _videoPlayer.Position;
        double ratio = 0;
        if (_videoPlayer.HasDuration)
        {
            ratio = Math.Clamp(position.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
        }

        _progressFill.SetBounds(0, 0, (int)Math.Round(width * ratio), height);
        _progressLabel.Text = $"{FormatTime(position)} / {FormatTime(duration)}   {(_videoPlayer.IsPlaying ? "Playing" : "Paused")}";
        _progressLabel.BringToFront();
    }

    private void ProgressPanel_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _singleClickTimer.Stop();
        _progressDragging = true;
        SeekFromProgressMouse(sender, e);
    }

    private void ProgressPanel_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_progressDragging)
        {
            return;
        }

        SeekFromProgressMouse(sender, e);
    }

    private void ProgressPanel_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        SeekFromProgressMouse(sender, e);
        _progressDragging = false;
    }

    private void SeekFromProgressMouse(object? sender, MouseEventArgs e)
    {
        if (!_videoPlayer.HasDuration || sender is not Control senderControl)
        {
            return;
        }

        Point panelPoint = _progressPanel.PointToClient(senderControl.PointToScreen(e.Location));
        double ratio = Math.Clamp(panelPoint.X / (double)Math.Max(1, _progressPanel.ClientSize.Width), 0, 1);
        _videoPlayer.Seek(TimeSpan.FromMilliseconds(_videoPlayer.Duration.TotalMilliseconds * ratio));
        UpdateProgressBar();
    }

    private void BuildContextMenu()
    {
        _contextMenu.Items.Add("打开图片(&O)", null, (_, _) => OpenImageWithDialog());
        _contextMenu.Items.Add("打开视频(&V)", null, (_, _) => OpenVideoWithDialog());
        _contextMenu.Items.Add("播放/暂停(空格)", null, (_, _) => ToggleVideoPlayback());
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
            _stretchMenuItem.Enabled = _image is not null || _videoPath is not null;
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
        ClearMedia();
        Invalidate();
    }

    private void PreviousPattern()
    {
        _patternRenderer.PreviousPattern();
        ClearMedia();
        Invalidate();
    }

    private void ClearMedia()
    {
        _image?.Dispose();
        _image = null;
        _imagePath = null;
        _videoPlayer.Close();
        _videoPath = null;
        _videoUiTimer.Stop();
        UpdateOverlayControls();
    }

    private void ToggleInfo()
    {
        _showInfo = !_showInfo;
        UpdateOverlayControls();
        Invalidate();
    }

    private void ToggleStretch()
    {
        _stretchImage = !_stretchImage;
        UpdateVideoDestination();
        UpdateOverlayControls();
        Invalidate();
    }

    private void ToggleTopMost()
    {
        _topMost = !_topMost;
        TopMost = _topMost;
        UpdateOverlayControls();
        Invalidate();
    }

    private void RedetectScreens()
    {
        _layout = ScreenLayoutInfo.Detect();
        ApplyVirtualDesktopBounds();
        UpdateVideoDestination();
        UpdateOverlayControls();
        Invalidate();
    }

    private void ToggleHelp()
    {
        _showHelp = !_showHelp;
        UpdateOverlayControls();
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

    private void OpenVideoWithDialog()
    {
        bool restoreTopMost = TopMost;
        TopMost = false;

        using OpenFileDialog dialog = new()
        {
            Title = "打开测试视频",
            Filter = "视频文件|*.mp4;*.m4v;*.mov|所有文件|*.*",
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

        LoadVideo(dialog.FileName);
    }

    private void OpenMediaWithDialog()
    {
        bool restoreTopMost = TopMost;
        TopMost = false;

        using OpenFileDialog dialog = new()
        {
            Title = "打开图片或视频",
            Filter = "图片和视频|*.bmp;*.gif;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.mp4;*.m4v;*.mov|图片文件|*.bmp;*.gif;*.jpg;*.jpeg;*.png;*.tif;*.tiff|视频文件|*.mp4;*.m4v;*.mov|所有文件|*.*",
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

        string extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
        if (IsVideoExtension(extension))
        {
            LoadVideo(dialog.FileName);
        }
        else
        {
            LoadImage(dialog.FileName);
        }
    }

    private void LoadImage(string path, bool updatePlaylist = true)
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

            ClearMedia();
            _image = bitmap;
            _imagePath = fullPath;
            if (updatePlaylist)
            {
                UpdateImagePlaylist(fullPath);
            }
            _stretchImage = false;
            UpdateOverlayControls();
            Invalidate();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"图片加载失败：{ex.Message}", "MosaicFallback", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void LoadVideo(string path)
    {
        string fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            MessageBox.Show($"视频不存在：{fullPath}", "MosaicFallback", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            ClearMedia();
            _videoPlayer.Open(fullPath);
            _videoPath = fullPath;
            _imagePlaylist = Array.Empty<string>();
            _imagePlaylistIndex = -1;
            _stretchImage = false;
            UpdateVideoDestination();
            _videoPlayer.Play();
            _videoUiTimer.Start();
            UpdateOverlayControls();
            Invalidate();
        }
        catch (Exception ex)
        {
            _videoPlayer.Close();
            _videoPath = null;
            MessageBox.Show($"视频加载失败：{ex.Message}", "MosaicFallback", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ToggleVideoPlayback()
    {
        if (_videoPath is null)
        {
            return;
        }

        _videoPlayer.TogglePlayPause();
        UpdateOverlayControls();
        Invalidate();
    }

    private void SwitchImage(int delta)
    {
        if (_imagePath is null)
        {
            return;
        }

        if (_imagePlaylist.Length == 0 || _imagePlaylistIndex < 0)
        {
            UpdateImagePlaylist(_imagePath);
        }

        if (_imagePlaylist.Length <= 1)
        {
            return;
        }

        int next = (_imagePlaylistIndex + delta) % _imagePlaylist.Length;
        if (next < 0)
        {
            next += _imagePlaylist.Length;
        }

        LoadImage(_imagePlaylist[next], updatePlaylist: false);
        _imagePlaylistIndex = next;
    }

    private void UpdateImagePlaylist(string selectedPath)
    {
        string? directory = Path.GetDirectoryName(selectedPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            _imagePlaylist = new[] { selectedPath };
            _imagePlaylistIndex = 0;
            return;
        }

        _imagePlaylist = Directory.EnumerateFiles(directory)
            .Where(path => IsImageExtension(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        _imagePlaylistIndex = Array.FindIndex(_imagePlaylist, path => string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase));
        if (_imagePlaylistIndex < 0)
        {
            _imagePlaylist = _imagePlaylist.Append(selectedPath).OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase).ToArray();
            _imagePlaylistIndex = Array.FindIndex(_imagePlaylist, path => string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void SeekVideoBy(TimeSpan delta)
    {
        if (_videoPath is null || !_videoPlayer.HasDuration)
        {
            return;
        }

        _videoPlayer.Seek(_videoPlayer.Position + delta);
        UpdateOverlayControls();
    }

    private void UpdateVideoDestination()
    {
        if (_videoPath is null)
        {
            return;
        }

        Rectangle hostBounds = new(0, 0, _layout.VirtualBounds.Width, _layout.VirtualBounds.Height);
        _videoPlayer.SetDestination(hostBounds, _stretchImage);
        UpdateOverlayControls();
    }

    private void HandleSingleClick()
    {
        if (_videoPath is not null)
        {
            ToggleVideoPlayback();
        }
        else if (_image is not null)
        {
            SwitchImage(1);
        }
        else
        {
            NextPattern();
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
        builder.AppendLine("快捷键：Esc/Q 退出 | O 打开图片 | V 打开视频 | 空格播放/暂停 | I 信息 | S 原始/拉伸 | P Pattern | ↑/↓ 图片或Pattern | ←/→ 视频进度 | F 置顶 | R 重检屏幕 | H 帮助");
        builder.AppendLine("鼠标：单击切换图片或 Pattern/播放暂停 | 双击打开图片或视频 | 右键菜单 | 滚轮切换图片或 Pattern");
        builder.AppendLine($"虚拟桌面：X={_layout.VirtualBounds.Left}, Y={_layout.VirtualBounds.Top}, W={_layout.VirtualBounds.Width}, H={_layout.VirtualBounds.Height}");
        builder.AppendLine($"模式：{GetModeText()} ({_patternRenderer.PatternName})");
        builder.AppendLine($"图片：{(_imagePath ?? "未选择")}");
        builder.AppendLine($"视频：{(_videoPath ?? "未选择")}");
        if (_image is not null)
        {
            builder.AppendLine($"图片尺寸：{_image.Width} x {_image.Height}");
        }
        if (_videoPath is not null)
        {
            builder.AppendLine($"视频尺寸：{(_videoPlayer.NaturalSize.IsEmpty ? "未知" : $"{_videoPlayer.NaturalSize.Width} x {_videoPlayer.NaturalSize.Height}")}");
            builder.AppendLine($"视频 DPI：{_videoPlayer.DpiScaleX:0.###} x {_videoPlayer.DpiScaleY:0.###}");
            builder.AppendLine($"视频状态：{(_videoPlayer.IsPlaying ? "播放" : "暂停")}");
            builder.AppendLine($"视频进度：{FormatTime(_videoPlayer.Position)} / {FormatTime(_videoPlayer.Duration)}");
        }
        builder.AppendLine($"显示：{(_stretchImage ? "拉伸铺满虚拟桌面" : "原始尺寸 pixel-to-pixel")}");
        builder.AppendLine($"置顶：{(_topMost ? "是" : "否")}");
        builder.AppendLine(_layout.ToDebugText());
        builder.AppendLine("Author: liaoweipeng");
        return builder.ToString().TrimEnd();
    }

    private string GetModeText()
    {
        if (_image is not null)
        {
            return "图片显示";
        }

        if (_videoPath is not null)
        {
            return "视频显示";
        }

        return "测试 pattern";
    }

    private static string FormatTime(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return "00:00";
        }

        return value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss")
            : value.ToString(@"mm\:ss");
    }

    private static bool IsImageExtension(string extension)
    {
        return ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsVideoExtension(string extension)
    {
        return VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
