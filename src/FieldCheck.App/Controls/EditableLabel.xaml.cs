using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FieldCheck.App.Controls;

/// <summary>
/// A label that turns into a text box for in-place renaming. Enter or losing focus commits the
/// change to the two-way <see cref="Text"/> binding; Escape reverts. Double-click or the pencil
/// button starts editing.
/// </summary>
public partial class EditableLabel : UserControl
{
    private bool _isEditing;

    public EditableLabel()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(EditableLabel),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void DisplayRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            BeginEdit();
    }

    private void EditButton_Click(object sender, RoutedEventArgs e) => BeginEdit();

    private void BeginEdit()
    {
        EditBox.Text = Text ?? string.Empty;
        _isEditing = true;
        DisplayRoot.Visibility = Visibility.Collapsed;
        EditBox.Visibility = Visibility.Visible;
        EditBox.Focus();
        EditBox.SelectAll();
    }

    private void EndEdit()
    {
        _isEditing = false;
        EditBox.Visibility = Visibility.Collapsed;
        DisplayRoot.Visibility = Visibility.Visible;
    }

    private void Commit()
    {
        if (!_isEditing)
            return;
        // Write through the two-way binding; a view model may coerce/revert (e.g. blank names).
        Text = EditBox.Text;
        EndEdit();
    }

    private void Cancel()
    {
        if (!_isEditing)
            return;
        EndEdit();
    }

    private void EditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Commit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Cancel();
            e.Handled = true;
        }
    }

    private void EditBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => Commit();
}
