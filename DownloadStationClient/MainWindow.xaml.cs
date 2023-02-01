using ModernWpf.Controls.Primitives;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DownloadStationClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static Color Blue { get; } = Color.FromArgb(255, 0, 151, 230);

        public static Color Green { get; } = Color.FromArgb(255, 76, 209, 55);

        public static Color Yellow { get; } = Color.FromArgb(255, 251, 197, 49);

        public static Color Red { get; } = Color.FromArgb(255, 232, 65, 24);

        public static Color Purple { get; } = Color.FromArgb(255, 190, 46, 221);

        public static Color Gray { get; } = Color.FromArgb(255, 113, 128, 147);


        public MainWindow()
        {
            App.Window = this;

            InitializeComponent();

            if (App.DEBUG)
                Title += "  (DEBUG)";
        }

        private void UserStackPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }

        private void ListViewItem_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

        }
    }
}
