using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RailworksDownloader.Properties;
using Microsoft.Win32;
using System.IO;
using ListViewItem = System.Windows.Controls.ListViewItem;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro RailworksPathDialog.xaml
    /// </summary>
    public partial class InstallPackageDialog : ContentDialog
    {
        public InstallPackageDialog()
        {
            InitializeComponent();           
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void ListViewItem_Selected(object sender, RoutedEventArgs e)
        {
            (sender as ListViewItem).IsSelected = true;
        }

        private void ListViewItem_Unselected(object sender, RoutedEventArgs e)
        {
            (sender as ListViewItem).IsSelected = false;
        }
    }
}
