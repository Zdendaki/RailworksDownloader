using Microsoft.Win32;
using ModernWpf.Controls;
using System.IO;
using System.Windows;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro RailworksPathDialog.xaml
    /// </summary>
    public partial class RailworksPathDialog : ContentDialog
    {
        public RailworksPathDialog()
        {
            InitializeComponent();

            UserPath.Text = App.Settings.RailworksLocation;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (UserPath.Text.Length > 3)
            {
                App.Settings.RailworksLocation = UserPath.Text;
                App.Settings.Save();
            }
            else
                args.Cancel = true;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "RailWorks|RailWorks.exe",
                FileName = "RailWorks.exe",
                Title = Localization.Strings.SelectRWPathTitle
            };

            if (ofd.ShowDialog() == true)
            {
                UserPath.Text = Path.GetDirectoryName(ofd.FileName);
            }
        }

        private void AutoButton_Click(object sender, RoutedEventArgs e)
        {
            UserPath.Text = App.SteamManager.RWPath;
        }
    }
}
