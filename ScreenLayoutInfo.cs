using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MosaicFallback;

public sealed class ScreenLayoutInfo
{
    private ScreenLayoutInfo(IReadOnlyList<ScreenInfo> screens, Rectangle virtualBounds)
    {
        Screens = screens;
        VirtualBounds = virtualBounds;
    }

    public IReadOnlyList<ScreenInfo> Screens { get; }

    public Rectangle VirtualBounds { get; }

    public static ScreenLayoutInfo Detect()
    {
        Screen[] allScreens = Screen.AllScreens
            .OrderBy(screen => screen.Bounds.Left)
            .ThenBy(screen => screen.Bounds.Top)
            .ToArray();

        if (allScreens.Length == 0)
        {
            Rectangle primary = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            return new ScreenLayoutInfo(
                new[] { new ScreenInfo(1, "Fallback", primary, primary, true) },
                primary);
        }

        Rectangle union = allScreens[0].Bounds;
        for (int i = 1; i < allScreens.Length; i++)
        {
            union = Rectangle.Union(union, allScreens[i].Bounds);
        }

        List<ScreenInfo> infos = new();
        for (int i = 0; i < allScreens.Length; i++)
        {
            Screen screen = allScreens[i];
            infos.Add(new ScreenInfo(
                i + 1,
                screen.DeviceName,
                screen.Bounds,
                screen.WorkingArea,
                screen.Primary));
        }

        return new ScreenLayoutInfo(infos, union);
    }

    public string ToDebugText()
    {
        StringBuilder builder = new();
        builder.AppendLine($"Virtual desktop: X={VirtualBounds.Left}, Y={VirtualBounds.Top}, W={VirtualBounds.Width}, H={VirtualBounds.Height}");

        foreach (ScreenInfo screen in Screens)
        {
            Rectangle b = screen.Bounds;
            builder.AppendLine(
                $"Screen {screen.Index}: {b.Width}x{b.Height}, X={b.Left}, Y={b.Top}, Right={b.Right}, Bottom={b.Bottom}, Primary={screen.IsPrimary}");
        }

        return builder.ToString().TrimEnd();
    }
}

public sealed record ScreenInfo(
    int Index,
    string DeviceName,
    Rectangle Bounds,
    Rectangle WorkingArea,
    bool IsPrimary);
