using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Vanara.PInvoke;

namespace TinyClips.Windows.Views;

public partial class RegionSelectionWindow : Window
{
    private Point _startPoint;
    private bool _isSelecting;

    public Int32Rect? Selection { get; private set; }

    public RegionSelectionWindow()
    {
        InitializeComponent();

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    public static Task<Int32Rect?> SelectRegionAsync(Window owner)
    {
        var selector = new RegionSelectionWindow { Owner = owner };
        var accepted = selector.ShowDialog();
        return Task.FromResult(accepted == true ? selector.Selection : null);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            User32.SetWindowPos(source.Handle,
                User32.SpecialWindowHandles.HWND_TOPMOST,
                0,
                0,
                0,
                0,
                User32.SetWindowPosFlags.SWP_NOMOVE |
                User32.SetWindowPosFlags.SWP_NOSIZE |
                User32.SetWindowPosFlags.SWP_SHOWWINDOW);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _isSelecting = true;
        _startPoint = e.GetPosition(this);
        Canvas.SetLeft(SelectionRect, _startPoint.X);
        Canvas.SetTop(SelectionRect, _startPoint.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        SelectionRect.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isSelecting)
        {
            return;
        }

        var current = e.GetPosition(this);
        var x = Math.Min(current.X, _startPoint.X);
        var y = Math.Min(current.Y, _startPoint.Y);
        var width = Math.Abs(current.X - _startPoint.X);
        var height = Math.Abs(current.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = width;
        SelectionRect.Height = height;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_isSelecting)
        {
            return;
        }

        _isSelecting = false;
        ReleaseMouseCapture();

        var current = e.GetPosition(this);
        var x = (int)Math.Round(Math.Min(current.X, _startPoint.X) + Left);
        var y = (int)Math.Round(Math.Min(current.Y, _startPoint.Y) + Top);
        var width = (int)Math.Round(Math.Abs(current.X - _startPoint.X));
        var height = (int)Math.Round(Math.Abs(current.Y - _startPoint.Y));

        Selection = new Int32Rect(x, y, width, height);
        DialogResult = width > 5 && height > 5;
        Close();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }

        base.OnPreviewKeyDown(e);
    }
}
