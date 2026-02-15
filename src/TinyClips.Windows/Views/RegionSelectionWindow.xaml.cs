using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Vanara.PInvoke;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace TinyClips.Windows.Views;

public partial class RegionSelectionWindow : Window
{
    private WpfPoint _startPoint;
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
                new HWND(-1),
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

    protected override void OnMouseMove(WpfMouseEventArgs e)
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
        var startScreen = PointToScreen(_startPoint);
        var currentScreen = PointToScreen(current);
        var x = (int)Math.Round(Math.Min(currentScreen.X, startScreen.X));
        var y = (int)Math.Round(Math.Min(currentScreen.Y, startScreen.Y));
        var width = (int)Math.Round(Math.Abs(currentScreen.X - startScreen.X));
        var height = (int)Math.Round(Math.Abs(currentScreen.Y - startScreen.Y));

        Selection = new Int32Rect(x, y, width, height);
        DialogResult = width > 5 && height > 5;
        Close();
    }

    protected override void OnPreviewKeyDown(WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }

        base.OnPreviewKeyDown(e);
    }
}
