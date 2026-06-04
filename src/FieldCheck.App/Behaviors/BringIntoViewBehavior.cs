using System.Windows;

namespace FieldCheck.App.Behaviors;

/// <summary>
/// When the bound <see cref="TriggerProperty"/> becomes true, scrolls the element into view.
/// Used to reveal a search-navigated item. Best-effort: if the element isn't realized yet, nothing happens.
/// </summary>
public static class BringIntoViewBehavior
{
    public static readonly DependencyProperty TriggerProperty =
        DependencyProperty.RegisterAttached("Trigger", typeof(bool), typeof(BringIntoViewBehavior),
            new PropertyMetadata(false, OnTriggerChanged));

    public static void SetTrigger(DependencyObject element, bool value) => element.SetValue(TriggerProperty, value);
    public static bool GetTrigger(DependencyObject element) => (bool)element.GetValue(TriggerProperty);

    private static void OnTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && d is FrameworkElement element)
            element.BringIntoView();
    }
}
