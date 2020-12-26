using ModernWpf.Controls;
using RailworksDownloader.Properties;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro LoginDialog.xaml
    /// </summary>
    public partial class LoginDialog : ContentDialog
    {
        PackageManager PM { get; set; }
        int Invoker { get; set; } //0 - DownloadDeps, 1 - CheckUpates
        Uri ApiUrl { get; set; }
        public LoginDialog(PackageManager pm, Uri apiUrl, int invoker)
        {
            InitializeComponent();
            PM = pm;
            ApiUrl = apiUrl;
            Invoker = invoker;
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
                        switch (Invoker)
                        {
                            case 0:
                                PM.DownloadDependencies();
                                break;
                            case 1:
                                PM.CheckUpdates();
                                break;
                        }
                    }
                    else
                    {
                        args.Cancel = true;
                        //FIXME: replace message box with better designed one
                        MessageBox.Show(result.message, "Login error!");
                    }
                }).Wait();
            }
        }
    }
}
