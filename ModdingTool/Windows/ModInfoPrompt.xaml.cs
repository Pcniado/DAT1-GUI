using MahApps.Metro.Controls;
using System.Windows;

namespace ModdingTool.Windows
{
    public partial class ModInfoPrompt : MetroWindow
    {
        public string ModName { get; private set; } = string.Empty;
        public string Author { get; private set; } = string.Empty;
        public ModInfoPrompt(string modName = "", string author = "")
        {
            InitializeComponent();
            ModNameBox.Text = modName;
            AuthorBox.Text = author;
        }
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ModName = ModNameBox.Text;
            Author = AuthorBox.Text;
            DialogResult = true;
            Close();
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 