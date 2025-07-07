using MahApps.Metro.Controls;
using System.Windows;

namespace ModdingTool.Windows
{
    public partial class CustomMessageBox : MetroWindow
    {
        public bool? Result { get; private set; } = null;

        public CustomMessageBox(string message, string title = "Message", bool showCancel = false)
        {
            InitializeComponent();
            MessageText.Text = message;
            Title = title;
            CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
            Close();
        }
    }
} 