using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FieldCheck.App.Services;
using FieldCheck.App.ViewModels;

namespace FieldCheck.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly AppStateService _state;
    private bool _restored;

    public MainWindow(MainViewModel viewModel, AppStateService state)
    {
        _viewModel = viewModel;
        _state = state;
        InitializeComponent();
        DataContext = viewModel;

        ApplyStoredGeometry();

        Loaded += OnLoaded;
        Closing += OnClosing;
        SizeChanged += OnGeometryChanged;
        LocationChanged += OnGeometryChanged;
        StateChanged += OnGeometryChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _restored = true;
        ApplySidebarState();
        _viewModel.NotifyLoaded();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        CaptureGeometry();
        _state.Flush();
    }

    private void ApplyStoredGeometry()
    {
        var settings = _state.State.Settings;
        if (settings.WindowWidth >= MinWidth)
            Width = settings.WindowWidth;
        if (settings.WindowHeight >= MinHeight)
            Height = settings.WindowHeight;

        if (settings.WindowLeft is { } left && settings.WindowTop is { } top &&
            IsOnScreen(left, top, Width, Height))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (settings.WindowMaximized)
            WindowState = WindowState.Maximized;
    }

    private void OnGeometryChanged(object? sender, EventArgs e)
    {
        if (!_restored)
            return;
        CaptureGeometry();
        _state.RequestSave();
    }

    private void CaptureGeometry()
    {
        var settings = _state.State.Settings;
        if (WindowState == WindowState.Normal)
        {
            settings.WindowWidth = ActualWidth;
            settings.WindowHeight = ActualHeight;
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
            settings.WindowMaximized = false;
        }
        else if (WindowState == WindowState.Maximized)
        {
            settings.WindowMaximized = true;
            var bounds = RestoreBounds;
            if (!bounds.IsEmpty)
            {
                settings.WindowWidth = bounds.Width;
                settings.WindowHeight = bounds.Height;
                settings.WindowLeft = bounds.Left;
                settings.WindowTop = bounds.Top;
            }
        }
    }

    /// <summary>Keeps a restored window from opening on a monitor that no longer exists.</summary>
    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var screenLeft = SystemParameters.VirtualScreenLeft;
        var screenTop = SystemParameters.VirtualScreenTop;
        var screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
        var screenBottom = screenTop + SystemParameters.VirtualScreenHeight;

        if (left + width <= screenLeft + 60 || left >= screenRight - 60)
            return false;
        if (top < screenTop - 1 || top >= screenBottom - 40)
            return false;
        return true;
    }

    private void ProjectMore_Click(object sender, RoutedEventArgs e) => OpenMenu(sender);

    private void ChecklistMore_Click(object sender, RoutedEventArgs e) => OpenMenu(sender);

    // Clicking a project row (anywhere the chevron/name-editor/action buttons didn't handle) selects
    // the project and shows its overview. Those child controls mark their own clicks handled, so this
    // only fires for "empty" header clicks and single-clicks on the name.
    private void ProjectHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ProjectViewModel project })
            _viewModel.SelectProject(project);
    }

    private static void OpenMenu(object sender)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.DataContext = button.DataContext;
            menu.IsOpen = true;
        }
    }

    // The checklist body holds nested item ListBoxes (each with its own scroll viewer) which would
    // otherwise swallow the mouse wheel. Handling it in the preview phase on the outer ScrollViewer
    // means the wheel always scrolls the checklist whenever the pointer is over the item area.
    private void MainScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }

    // ---------------------------------------------------------------- sidebar (resize + collapse)

    private const double SidebarMinWidth = 240;
    private const double SidebarMaxWidth = 500;

    private void ApplySidebarState()
    {
        var settings = _state.State.Settings;
        if (settings.SidebarCollapsed)
        {
            SidebarColumn.MinWidth = 0;
            SidebarColumn.MaxWidth = 0;
            SidebarColumn.Width = new GridLength(0);
            SidebarPanel.Visibility = Visibility.Collapsed;
            SidebarSplitter.Visibility = Visibility.Collapsed;
        }
        else
        {
            var width = Math.Clamp(settings.SidebarWidth <= 0 ? 300 : settings.SidebarWidth, SidebarMinWidth, SidebarMaxWidth);
            SidebarColumn.MinWidth = SidebarMinWidth;
            SidebarColumn.MaxWidth = SidebarMaxWidth;
            SidebarColumn.Width = new GridLength(width);
            SidebarPanel.Visibility = Visibility.Visible;
            SidebarSplitter.Visibility = Visibility.Visible;
        }
        SidebarToggleButton.ToolTip = settings.SidebarCollapsed ? "Show sidebar" : "Hide sidebar";
    }

    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        var settings = _state.State.Settings;
        settings.SidebarCollapsed = !settings.SidebarCollapsed;
        ApplySidebarState();
        _state.RequestSave();
    }

    private void SidebarPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Persist a user resize (via the splitter). Guarded so initial layout / restore doesn't spam saves.
        if (!_restored)
            return;
        var settings = _state.State.Settings;
        if (settings.SidebarCollapsed)
            return;
        var width = SidebarColumn.ActualWidth;
        if (width >= SidebarMinWidth && width <= SidebarMaxWidth && Math.Abs(width - settings.SidebarWidth) > 0.5)
        {
            settings.SidebarWidth = width;
            _state.RequestSave();
        }
    }
}
