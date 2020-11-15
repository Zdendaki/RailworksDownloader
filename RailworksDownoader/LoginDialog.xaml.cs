using ModernWpf.Controls;
using Newtonsoft.Json.Linq;
using RailworksDownloader.Properties;
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

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro LoginDialog.xaml
    /// </summary>
    public partial class LoginDialog : ContentDialog
    {
        PackageManager PM { get; set; }
        Uri ApiUrl { get; set; }
        public LoginDialog(PackageManager pm, Uri apiUrl)
        {
            InitializeComponent();
            PM = pm;
            ApiUrl = apiUrl;
            ShowAsync();
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string login = Username.Text;
            string pass = Password.Password;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(pass))
            {
                args.Cancel = true;
            }
            else
            {
                Task.Run(async () =>
                {
                    ObjectResult<LoginContent> result = await WebWrapper.Login(login.Trim(), pass, ApiUrl);
                    if (result != null && result.code == 1 && result.content?.privileges >= 0)
                    {
                        Settings.Default.Username = login.Trim();
                        Settings.Default.Password = Utils.PasswordEncryptor.Encrypt(pass, login.Trim());
                        Settings.Default.Save();
                        PM.DownloadDependencies();
                    } else
                        args.Cancel = true;
                }).Wait();
            }
        }
    }
}
