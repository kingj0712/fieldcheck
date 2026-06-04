using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace FieldCheck.App.Behaviors;

/// <summary>The payload handed to the reorder command: move <see cref="Source"/> relative to <see cref="Target"/>.</summary>
public sealed record ReorderRequest(object Source, object? Target, bool PlaceBelow);

/// <summary>
/// Attached behavior that turns any ListBox into a drag-to-reorder list. It carries the dragged
/// data item, paints a subtle insertion line, and on drop invokes a command with a
/// <see cref="ReorderRequest"/>. Clicks on checkboxes/buttons never start a drag, and the whole
/// behavior can be switched off (via <see cref="IsEnabledProperty"/>) while a search filter is active.
/// </summary>
public static class DragDropReorderBehavior
{
    private const string DataFormat = "FieldCheck.ReorderItem";

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(DragDropReorderBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty DropCommandProperty =
        DependencyProperty.RegisterAttached("DropCommand", typeof(System.Windows.Input.ICommand), typeof(DragDropReorderBehavior),
            new PropertyMetadata(null));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetDropCommand(DependencyObject element, System.Windows.Input.ICommand? value) => element.SetValue(DropCommandProperty, value);
    public static System.Windows.Input.ICommand? GetDropCommand(DependencyObject element) => (System.Windows.Input.ICommand?)element.GetValue(DropCommandProperty);

    private static readonly ConditionalWeakTable<ListBox, DragState> States = new();

    private sealed class DragState
    {
        public Point Start;
        public bool Armed;
        public object? Data;
        public InsertionAdorner? Adorner;
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox list)
            return;

        // Hook handlers once; the IsEnabled flag is consulted live at drag time.
        if (e.OldValue is false && e.NewValue is true && !States.TryGetValue(list, out _))
        {
            States.Add(list, new DragState());
            list.AllowDrop = true;
            list.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            list.PreviewMouseMove += OnPreviewMouseMove;
            list.DragOver += OnDragOver;
            list.DragLeave += OnDragLeave;
            list.Drop += OnDrop;
        }
    }

    private static DragState State(ListBox list) => States.GetValue(list, _ => new DragState());

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var list = (ListBox)sender;
        var state = State(list);
        state.Armed = false;
        state.Data = null;

        if (!GetIsEnabled(list))
            return;

        // Don't begin a drag from an interactive control (checkbox, button, text box).
        if (IsWithinInteractiveControl(e.OriginalSource as DependencyObject))
            return;

        var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (container is null)
            return;

        state.Start = e.GetPosition(list);
        state.Data = container.DataContext;
        state.Armed = true;
    }

    private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        var list = (ListBox)sender;
        var state = State(list);

        if (!state.Armed || state.Data is null || e.LeftButton != MouseButtonState.Pressed)
            return;
        if (!GetIsEnabled(list))
            return;

        var pos = e.GetPosition(list);
        if (Math.Abs(pos.X - state.Start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - state.Start.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var data = state.Data;
        state.Armed = false;
        try
        {
            var payload = new DataObject(DataFormat, data);
            DragDrop.DoDragDrop(list, payload, DragDropEffects.Move);
        }
        finally
        {
            RemoveAdorner(state);
            state.Data = null;
        }
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        var list = (ListBox)sender;
        if (!e.Data.GetDataPresent(DataFormat) || !GetIsEnabled(list))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        var state = State(list);
        var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (container is null)
        {
            RemoveAdorner(state);
            return;
        }

        var below = e.GetPosition(container).Y > container.ActualHeight / 2;
        ShowAdorner(state, container, below);
    }

    private static void OnDragLeave(object sender, DragEventArgs e)
    {
        var list = (ListBox)sender;
        RemoveAdorner(State(list));
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        var list = (ListBox)sender;
        var state = State(list);
        RemoveAdorner(state);

        if (!e.Data.GetDataPresent(DataFormat) || !GetIsEnabled(list))
            return;

        var source = e.Data.GetData(DataFormat);
        if (source is null)
            return;

        var command = GetDropCommand(list);
        if (command is null)
            return;

        object? target = null;
        var placeBelow = false;
        var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (container is not null)
        {
            target = container.DataContext;
            placeBelow = e.GetPosition(container).Y > container.ActualHeight / 2;
        }

        if (ReferenceEquals(source, target))
            return;

        var request = new ReorderRequest(source, target, placeBelow);
        if (command.CanExecute(request))
            command.Execute(request);

        e.Handled = true;
    }

    private static void ShowAdorner(DragState state, ListBoxItem container, bool below)
    {
        if (state.Adorner is not null && ReferenceEquals(state.Adorner.AdornedElement, container))
        {
            state.Adorner.PlaceBelow = below;
            state.Adorner.InvalidateVisual();
            return;
        }

        RemoveAdorner(state);
        var layer = AdornerLayer.GetAdornerLayer(container);
        if (layer is null)
            return;

        var adorner = new InsertionAdorner(container) { PlaceBelow = below };
        layer.Add(adorner);
        state.Adorner = adorner;
    }

    private static void RemoveAdorner(DragState state)
    {
        if (state.Adorner is null)
            return;
        var layer = AdornerLayer.GetAdornerLayer(state.Adorner.AdornedElement);
        layer?.Remove(state.Adorner);
        state.Adorner = null;
    }

    private static bool IsWithinInteractiveControl(DependencyObject? source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.Primitives.TextBoxBase)
                return true;
            if (current is ListBoxItem)
                return false;
        }
        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? from) where T : DependencyObject
    {
        for (var current = from; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is T match)
                return match;
        return null;
    }
}

/// <summary>A thin accent line drawn at the top or bottom edge of an item to show the drop position.</summary>
internal sealed class InsertionAdorner : Adorner
{
    public bool PlaceBelow { get; set; }

    public InsertionAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var brush = (Application.Current.TryFindResource("Accent") as Brush) ?? Brushes.SteelBlue;
        var pen = new Pen(brush, 2);
        var width = ((UIElement)AdornedElement).RenderSize.Width;
        var height = ((UIElement)AdornedElement).RenderSize.Height;
        var y = PlaceBelow ? height : 0;
        drawingContext.DrawLine(pen, new Point(2, y), new Point(width - 2, y));
    }
}
