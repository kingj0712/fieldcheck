using System.Windows;
using System.Windows.Controls;

namespace FieldCheck.App.Views;

/// <summary>
/// A flexible single dialog used for rename (one field), new checklist (name + project),
/// and add/edit item (text + section with reuse chips + notes). Field 1 is always required.
/// </summary>
public partial class EntryDialog : Window
{
    public EntryDialog()
    {
        InitializeComponent();
    }

    public string Field1Text => Field1Box.Text.Trim();
    public string Field2Text => Field2Box.Text.Trim();
    public string Field3Text => Field3Box.Text.Trim();
    public string NotesText => NotesBox.Text.Trim();

    public void Configure(
        string title,
        string field1Label, string field1Value,
        bool showField2 = false, string field2Label = "", string field2Value = "",
        bool showField3 = false, string field3Label = "", string field3Value = "",
        bool showNotes = false, string notesValue = "",
        IEnumerable<string>? chips = null,
        string confirmText = "Save")
    {
        Title = title;
        HeadingText.Text = title;

        Field1Label.Text = field1Label;
        Field1Box.Text = field1Value;

        Field2Panel.Visibility = showField2 ? Visibility.Visible : Visibility.Collapsed;
        Field2Label.Text = field2Label;
        Field2Box.Text = field2Value;

        var chipList = chips?.ToList() ?? new List<string>();
        ChipsList.ItemsSource = chipList;
        ChipsList.Visibility = chipList.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        Field3Panel.Visibility = showField3 ? Visibility.Visible : Visibility.Collapsed;
        Field3Label.Text = field3Label;
        Field3Box.Text = field3Value;

        NotesPanel.Visibility = showNotes ? Visibility.Visible : Visibility.Collapsed;
        NotesBox.Text = notesValue;

        OkButton.Content = confirmText;
        UpdateOkEnabled();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Field1Box.Focus();
        Field1Box.SelectAll();
    }

    private void Field1Box_TextChanged(object sender, TextChangedEventArgs e) => UpdateOkEnabled();

    private void UpdateOkEnabled() => OkButton.IsEnabled = Field1Box.Text.Trim().Length > 0;

    private void Chip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string section })
            Field2Box.Text = section;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Field1Box.Text.Trim().Length == 0)
            return;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
