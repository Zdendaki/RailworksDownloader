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
using System.Windows.Shapes;

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
