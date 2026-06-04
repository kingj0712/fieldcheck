using System.Windows;

namespace FieldCheck.App.Views;

public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    /// <summary>Single-button informational dialog.</summary>
    public void AsMessage(string heading, string message)
    {
        Title = heading;
        HeadingText.Text = heading;
        MessageText.Text = message;
        CancelButton.Visibility = Visibility.Collapsed;
        ConfirmButton.Content = "OK";
    }

    /// <summary>Two-button confirmation dialog. The confirm button turns red when destructive.</summary>
    public void AsConfirm(string heading, string message, string confirmText, bool destructive)
    {
        Title = heading;
        HeadingText.Text = heading;
        MessageText.Text = message;
        CancelButton.Visibility = Visibility.Visible;
        ConfirmButton.Content = confirmText;
        if (destructive)
            ConfirmButton.Style = (Style)FindResource("DangerButton");
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
