using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FieldCheck.Core.Models;
using FieldCheck.Core.Services;

namespace FieldCheck.App.Views;

/// <summary>Command-palette style global search over the whole workspace.</summary>
public partial class GlobalSearchDialog : Window
{
    private readonly AppState _state;

    public GlobalSearchDialog(AppState state)
    {
        InitializeComponent();
        _state = state;
        Loaded += (_, _) =>
        {
            SearchBox.Focus();
            UpdateResults();
        };
    }

    /// <summary>The result the user chose (Enter / double-click), or null if cancelled.</summary>
    public SearchResult? SelectedResult { get; private set; }

    private void UpdateResults()
    {
        var query = SearchBox.Text;
        var results = SearchService.Search(_state, query);
        ResultsList.ItemsSource = results;

        var blank = string.IsNullOrWhiteSpace(query);
        DefaultState.Visibility = blank ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = (!blank && results.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
        ResultsList.Visibility = results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (results.Count > 0)
            ResultsList.SelectedIndex = 0;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateResults();

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                OpenSelected();
                e.Handled = true;
                break;
            case Key.Escape:
                DialogResult = false;
                Close();
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        var count = ResultsList.Items.Count;
        if (count == 0)
            return;
        var index = ResultsList.SelectedIndex < 0 ? 0 : ResultsList.SelectedIndex + delta;
        ResultsList.SelectedIndex = Math.Clamp(index, 0, count - 1);
        if (ResultsList.SelectedItem is not null)
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        if (ResultsList.SelectedItem is SearchResult result)
        {
            SelectedResult = result;
            DialogResult = true;
            Close();
        }
    }
}
