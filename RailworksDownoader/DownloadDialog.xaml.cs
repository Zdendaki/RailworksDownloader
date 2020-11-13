using ModernWpf.Controls;
using System.Net;
using System;
using System.Windows;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro DownloadDialog.xaml
    /// </summary>
    public partial class DownloadDialog : ContentDialog
    {
        public DownloadDialog()
        {
            InitializeComponent();
        }

        private void DownloadFile()
        {
            string filename = "";
            string path = "";

            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
                    wc.DownloadFileCompleted += Wc_DownloadFileCompleted;
                    wc.DownloadFileAsync(new Uri(""), path);
                }
            }
            catch
            {
                MessageBox.Show("Unable to download file", "Error occured while downloading", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Wc_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
                MessageBox.Show(e.Error.Message, "Error occured while downloading", MessageBoxButton.OK, MessageBoxImage.Error);
            else if (!e.Cancelled)
                MessageBox.Show("File downloaded!", "Download complete", MessageBoxButton.OK, MessageBoxImage.Information);

            
        }

        private void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {

        }
    }
}
