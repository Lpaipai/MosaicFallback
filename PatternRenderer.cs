using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MosaicFallback;

public sealed class PatternRenderer
{
    private const int PatternCount = 5;
    private readonly Font _largeFont = new("Segoe UI", 42, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font _mediumFont = new("Consolas", 26, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly Font _smallFont = new("Consolas", 18, FontStyle.Regular, GraphicsUnit.Pixel);

    public int PatternIndex { get; private set; }

    public string PatternName => PatternIndex switch
    {
        1 => "cross frame + diagonals",
        2 => "horizontal gradient",
        3 => "vertical gradient",
        4 => "colorbar",
        _ => "diagnostic home"
    };

    public void NextPattern()
    {
        PatternIndex = (PatternIndex + 1) % PatternCount;
    }

    public void PreviousPattern()
    {
        PatternIndex = (PatternIndex + PatternCount - 1) % PatternCount;
    }

    public void Draw(Graphics g, ScreenLayoutInfo layout, Rectangle clientBounds, bool showInfo)
    {
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        switch (PatternIndex)
        {
            case 1:
                DrawCrossFramePattern(g, clientBounds);
                break;
            case 2:
                DrawHorizontalGradientPattern(g, clientBounds);
                break;
            case 3:
                DrawVerticalGradientPattern(g, clientBounds);
                break;
            case 4:
                DrawColorbarPattern(g, clientBounds);
                break;
            default:
                DrawFullDiagnosticPattern(g, clientBounds);
                DrawScreenFrames(g, layout, showInfo);
                break;
        }
    }

    private void DrawFullDiagnosticPattern(Graphics g, Rectangle bounds)
    {
        using LinearGradientBrush horizontal = new(bounds, Color.Black, Color.White, LinearGradientMode.Horizontal);
        g.FillRectangle(horizontal, bounds);

        using LinearGradientBrush vertical = new(bounds, Color.FromArgb(70, 255, 0, 0), Color.FromArgb(70, 0, 0, 255), LinearGradientMode.Vertical);
        g.FillRectangle(vertical, bounds);

        DrawGrid(g, bounds, 240, Color.FromArgb(95, 255, 255, 255));
        DrawGrid(g, bounds, 960, Color.FromArgb(180, 255, 255, 0));
        DrawCenterLines(g, bounds);
        DrawPixelLineBlocks(g, bounds);
        DrawRgbBlocks(g, bounds);
        DrawCheckerboard(g, new Rectangle(bounds.Right - 640, bounds.Bottom - 640, 560, 560), 20);
    }

    private static void DrawCrossFramePattern(Graphics g, Rectangle bounds)
    {
        g.Clear(Color.Black);

        using Pen white = new(Color.White, 3);
        Rectangle edge = new(bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1);
        int centerX = bounds.Left + bounds.Width / 2;
        int centerY = bounds.Top + bounds.Height / 2;

        g.DrawRectangle(white, edge);
        g.DrawLine(white, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
        g.DrawLine(white, bounds.Right - 1, bounds.Top, bounds.Left, bounds.Bottom - 1);
        g.DrawLine(white, centerX, bounds.Top, centerX, bounds.Bottom - 1);
        g.DrawLine(white, bounds.Left, centerY, bounds.Right - 1, centerY);
    }

    private static void DrawHorizontalGradientPattern(Graphics g, Rectangle bounds)
    {
        Rectangle[] rows = SplitRows(bounds, 4);
        FillHorizontalChannel(g, rows[0], Color.Black, Color.White);
        FillHorizontalChannel(g, rows[1], Color.Black, Color.Red);
        FillHorizontalChannel(g, rows[2], Color.Black, Color.Lime);
        FillHorizontalChannel(g, rows[3], Color.Black, Color.Blue);
    }

    private static void DrawVerticalGradientPattern(Graphics g, Rectangle bounds)
    {
        Rectangle[] columns = SplitColumns(bounds, 4);
        FillVerticalChannel(g, columns[0], Color.Black, Color.White);
        FillVerticalChannel(g, columns[1], Color.Black, Color.Red);
        FillVerticalChannel(g, columns[2], Color.Black, Color.Lime);
        FillVerticalChannel(g, columns[3], Color.Black, Color.Blue);
    }

    private static void DrawColorbarPattern(Graphics g, Rectangle bounds)
    {
        Color[] bars =
        {
            Color.White, Color.Yellow, Color.Cyan, Color.Lime,
            Color.Magenta, Color.Red, Color.Blue, Color.Black
        };

        Rectangle[] columns = SplitColumns(bounds, bars.Length);
        for (int i = 0; i < columns.Length; i++)
        {
            using SolidBrush brush = new(bars[i]);
            g.FillRectangle(brush, columns[i]);
        }
    }

    private static Rectangle[] SplitRows(Rectangle bounds, int count)
    {
        Rectangle[] rows = new Rectangle[count];
        for (int i = 0; i < count; i++)
        {
            int top = bounds.Top + bounds.Height * i / count;
            int bottom = bounds.Top + bounds.Height * (i + 1) / count;
            rows[i] = new Rectangle(bounds.Left, top, bounds.Width, bottom - top);
        }

        return rows;
    }

    private static Rectangle[] SplitColumns(Rectangle bounds, int count)
    {
        Rectangle[] columns = new Rectangle[count];
        for (int i = 0; i < count; i++)
        {
            int left = bounds.Left + bounds.Width * i / count;
            int right = bounds.Left + bounds.Width * (i + 1) / count;
            columns[i] = new Rectangle(left, bounds.Top, right - left, bounds.Height);
        }

        return columns;
    }

    private static void FillHorizontalChannel(Graphics g, Rectangle rect, Color left, Color right)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        using LinearGradientBrush brush = new(rect, left, right, LinearGradientMode.Horizontal);
        g.FillRectangle(brush, rect);
    }

    private static void FillVerticalChannel(Graphics g, Rectangle rect, Color top, Color bottom)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        using LinearGradientBrush brush = new(rect, top, bottom, LinearGradientMode.Vertical);
        g.FillRectangle(brush, rect);
    }

    private static void DrawGrid(Graphics g, Rectangle bounds, int spacing, Color color)
    {
        using Pen pen = new(color, 1);
        for (int x = AlignToSpacing(bounds.Left, spacing); x <= bounds.Right; x += spacing)
        {
            g.DrawLine(pen, x, bounds.Top, x, bounds.Bottom);
        }

        for (int y = AlignToSpacing(bounds.Top, spacing); y <= bounds.Bottom; y += spacing)
        {
            g.DrawLine(pen, bounds.Left, y, bounds.Right, y);
        }
    }

    private static int AlignToSpacing(int value, int spacing)
    {
        int remainder = value % spacing;
        return remainder == 0 ? value : value - remainder;
    }

    private static void DrawCenterLines(Graphics g, Rectangle bounds)
    {
        using Pen cyan = new(Color.Cyan, 3);
        using Pen magenta = new(Color.Magenta, 3);
        int centerX = bounds.Left + bounds.Width / 2;
        int centerY = bounds.Top + bounds.Height / 2;
        g.DrawLine(cyan, centerX, bounds.Top, centerX, bounds.Bottom);
        g.DrawLine(magenta, bounds.Left, centerY, bounds.Right, centerY);
    }

    private void DrawPixelLineBlocks(Graphics g, Rectangle bounds)
    {
        Rectangle block = new(bounds.Left + 80, bounds.Bottom - 720, 720, 560);
        g.FillRectangle(Brushes.Black, block);

        using Pen white = new(Color.White, 1);
        using Pen red = new(Color.Red, 1);
        using Pen green = new(Color.Lime, 1);
        using Pen blue = new(Color.Blue, 1);

        for (int i = 0; i < 160; i += 2)
        {
            g.DrawLine(white, block.Left, block.Top + i, block.Right, block.Top + i);
            g.DrawLine(red, block.Left + i, block.Top + 180, block.Left + i, block.Bottom);
        }

        g.DrawLine(green, block.Left, block.Top + 250, block.Right, block.Top + 250);
        g.DrawLine(blue, block.Left + 360, block.Top, block.Left + 360, block.Bottom);
        DrawLabel(g, "1 pixel H/V line test", block.Left + 18, block.Top + 18, _mediumFont, Color.White);
    }

    private void DrawRgbBlocks(Graphics g, Rectangle bounds)
    {
        int size = 180;
        int x = bounds.Left + 80;
        int y = bounds.Top + 180;
        Color[] colors = { Color.Red, Color.Lime, Color.Blue, Color.White, Color.Black };
        string[] labels = { "R", "G", "B", "W", "K" };

        for (int i = 0; i < colors.Length; i++)
        {
            Rectangle rect = new(x + i * (size + 24), y, size, size);
            using SolidBrush brush = new(colors[i]);
            g.FillRectangle(brush, rect);
            g.DrawRectangle(Pens.White, rect);
            DrawLabel(g, labels[i], rect.Left + 64, rect.Top + 52, _largeFont, i == 4 ? Color.White : Color.Black);
        }
    }

    private static void DrawCheckerboard(Graphics g, Rectangle rect, int cell)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        using SolidBrush light = new(Color.White);
        using SolidBrush dark = new(Color.Black);
        for (int y = rect.Top; y < rect.Bottom; y += cell)
        {
            for (int x = rect.Left; x < rect.Right; x += cell)
            {
                bool isLight = ((x - rect.Left) / cell + (y - rect.Top) / cell) % 2 == 0;
                g.FillRectangle(isLight ? light : dark, x, y, Math.Min(cell, rect.Right - x), Math.Min(cell, rect.Bottom - y));
            }
        }

        g.DrawRectangle(Pens.Lime, rect);
    }

    private void DrawScreenFrames(Graphics g, ScreenLayoutInfo layout, bool showInfo)
    {
        foreach (ScreenInfo screen in layout.Screens)
        {
            Rectangle b = screen.Bounds;
            using Pen border = new(Color.Lime, 8);
            g.DrawRectangle(border, b.Left + 4, b.Top + 4, b.Width - 8, b.Height - 8);

            using Pen split = new(Color.Yellow, 3);
            g.DrawLine(split, b.Left, b.Top, b.Left, b.Bottom);
            g.DrawLine(split, b.Right - 1, b.Top, b.Right - 1, b.Bottom);

            if (!showInfo)
            {
                continue;
            }

            Rectangle labelRect = new(b.Left + 24, b.Top + 24, Math.Min(780, b.Width - 48), 170);
            using SolidBrush background = new(Color.FromArgb(200, 0, 0, 0));
            g.FillRectangle(background, labelRect);
            g.DrawRectangle(Pens.Lime, labelRect);

            DrawLabel(g, $"Screen {screen.Index}", labelRect.Left + 18, labelRect.Top + 12, _largeFont, Color.Lime);
            DrawLabel(g, $"{b.Width} x {b.Height}", labelRect.Left + 22, labelRect.Top + 72, _mediumFont, Color.White);
            DrawLabel(g, $"X={b.Left}, Y={b.Top}", labelRect.Left + 22, labelRect.Top + 112, _mediumFont, Color.White);
        }
    }

    public void DrawOverlay(Graphics g, ScreenLayoutInfo layout, string text, Point location)
    {
        SizeF size = g.MeasureString(text, _smallFont, 1200);
        RectangleF rect = new(location.X, location.Y, size.Width + 28, size.Height + 24);
        using SolidBrush background = new(Color.FromArgb(220, 0, 0, 0));
        g.FillRectangle(background, rect);
        g.DrawRectangle(Pens.White, Rectangle.Round(rect));
        using SolidBrush foreground = new(Color.White);
        g.DrawString(text, _smallFont, foreground, new RectangleF(location.X + 14, location.Y + 12, size.Width, size.Height));
    }

    private static void DrawLabel(Graphics g, string text, int x, int y, Font font, Color color)
    {
        using SolidBrush shadow = new(Color.FromArgb(180, 0, 0, 0));
        using SolidBrush foreground = new(color);
        g.DrawString(text, font, shadow, x + 2, y + 2);
        g.DrawString(text, font, foreground, x, y);
    }
}
