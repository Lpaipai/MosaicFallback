using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;
using WinFormsControl = System.Windows.Forms.Control;

namespace MosaicFallback;

public sealed class WpfVideoPlayer : IDisposable
{
    private readonly ElementHost _host;
    private readonly Grid _root;
    private readonly MediaElement _mediaElement;

    public WpfVideoPlayer(WinFormsControl parent)
    {
        _root = new Grid
        {
            Background = System.Windows.Media.Brushes.Black,
            ClipToBounds = true
        };

        _mediaElement = new MediaElement
        {
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Manual,
            Stretch = Stretch.None,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        _mediaElement.MediaOpened += (_, _) =>
        {
            NaturalSize = new DrawingSize(
                Math.Max(1, _mediaElement.NaturalVideoWidth),
                Math.Max(1, _mediaElement.NaturalVideoHeight));
            MediaOpened?.Invoke(this, EventArgs.Empty);
        };
        _mediaElement.MediaEnded += (_, _) =>
        {
            _mediaElement.Position = TimeSpan.Zero;
            _mediaElement.Play();
            IsPlaying = true;
        };
        _mediaElement.MediaFailed += (_, e) =>
        {
            PlaybackFailed?.Invoke(this, e.ErrorException.Message);
        };

        _root.Children.Add(_mediaElement);
        _host = new ElementHost
        {
            Child = _root,
            Visible = false
        };
        parent.Controls.Add(_host);
        _host.BringToFront();
    }

    public event EventHandler? MediaOpened;

    public event EventHandler<string>? PlaybackFailed;

    public System.Windows.Forms.Control HostControl => _host;

    public string? FilePath { get; private set; }

    public DrawingSize NaturalSize { get; private set; }

    public double DpiScaleX { get; private set; } = 1.0;

    public double DpiScaleY { get; private set; } = 1.0;

    public bool IsPlaying { get; private set; }

    public TimeSpan Position
    {
        get
        {
            return FilePath is null ? TimeSpan.Zero : _mediaElement.Position;
        }
    }

    public TimeSpan Duration
    {
        get
        {
            if (FilePath is null || !_mediaElement.NaturalDuration.HasTimeSpan)
            {
                return TimeSpan.Zero;
            }

            return _mediaElement.NaturalDuration.TimeSpan;
        }
    }

    public bool HasDuration => FilePath is not null && _mediaElement.NaturalDuration.HasTimeSpan && Duration > TimeSpan.Zero;

    public void Open(string filePath)
    {
        Close();
        FilePath = filePath;
        NaturalSize = DrawingSize.Empty;
        _host.Visible = true;
        _mediaElement.Source = new Uri(filePath, UriKind.Absolute);
    }

    public void Play()
    {
        if (FilePath is null)
        {
            return;
        }

        _mediaElement.Play();
        IsPlaying = true;
    }

    public void Pause()
    {
        if (FilePath is null)
        {
            return;
        }

        _mediaElement.Pause();
        IsPlaying = false;
    }

    public void TogglePlayPause()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    public void SetDestination(DrawingRectangle hostBounds, bool stretch)
    {
        if (FilePath is null)
        {
            return;
        }

        _host.Bounds = hostBounds;
        UpdateDpiScale();
        _root.Width = ToDeviceIndependentPixels(hostBounds.Width, DpiScaleX);
        _root.Height = ToDeviceIndependentPixels(hostBounds.Height, DpiScaleY);

        if (stretch || NaturalSize.IsEmpty)
        {
            _mediaElement.Stretch = Stretch.Fill;
            _mediaElement.Width = ToDeviceIndependentPixels(hostBounds.Width, DpiScaleX);
            _mediaElement.Height = ToDeviceIndependentPixels(hostBounds.Height, DpiScaleY);
        }
        else
        {
            _mediaElement.Stretch = Stretch.None;
            _mediaElement.Width = ToDeviceIndependentPixels(NaturalSize.Width, DpiScaleX);
            _mediaElement.Height = ToDeviceIndependentPixels(NaturalSize.Height, DpiScaleY);
        }
    }

    private void UpdateDpiScale()
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(_root);
        DpiScaleX = NormalizeDpiScale(dpi.DpiScaleX);
        DpiScaleY = NormalizeDpiScale(dpi.DpiScaleY);
    }

    private static double ToDeviceIndependentPixels(int physicalPixels, double dpiScale)
    {
        return Math.Max(1.0, physicalPixels / NormalizeDpiScale(dpiScale));
    }

    private static double NormalizeDpiScale(double dpiScale)
    {
        return double.IsFinite(dpiScale) && dpiScale > 0 ? dpiScale : 1.0;
    }

    public void Seek(TimeSpan position)
    {
        if (FilePath is null || !HasDuration)
        {
            return;
        }

        TimeSpan duration = Duration;
        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }
        else if (position > duration)
        {
            position = duration;
        }

        _mediaElement.Position = position;
    }

    public void Close()
    {
        if (FilePath is null)
        {
            _host.Visible = false;
            return;
        }

        _mediaElement.Stop();
        _mediaElement.Source = null;
        _host.Visible = false;
        FilePath = null;
        NaturalSize = DrawingSize.Empty;
        IsPlaying = false;
    }

    public void Dispose()
    {
        Close();
        _host.Dispose();
    }
}
