using ModernWpf.Controls;
using System.Windows;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro ConflictPackageDialog.xaml
    /// </summary>
    public partial class ConflictPackageDialog : ContentDialog
    {
        public bool RewriteAll { get; set; }

        public bool KeepAll { get; set; }

        public bool RewriteLocal { get; set; }

        public bool KeepLocal { get; set; }

        public ConflictPackageDialog(string packageName)
        {
            InitializeComponent();

            PackageName.Text = packageName;
            Owner = App.Window;

            KeepAll = RewriteAll = KeepLocal = RewriteLocal = false;
        }

        private void OverwriteAll_Click(object sender, RoutedEventArgs e)
        {
            RewriteAll = true;
            Hide();
        }

        private void KeepAll_Click(object sender, RoutedEventArgs e)
        {
            KeepAll = true;
            Hide();
        }

        private void OverwriteLocal_Click(object sender, RoutedEventArgs e)
        {
            RewriteLocal = true;
            Hide();
        }

        private void KeepLocal_Click(object sender, RoutedEventArgs e)
        {
            KeepLocal = true;
            Hide();
        }
    }
}
