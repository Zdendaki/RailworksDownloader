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
        private PackageManager PM { get; set; }
        private int Invoker { get; set; } //0 - DownloadDeps, 1 - CheckUpates
        private Uri ApiUrl { get; set; }

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
                ErrorLabel.Content = "You haven't filled all required fields.";
                ErrorLabel.Visibility = Visibility.Visible;
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

                        new Task(() =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                ErrorLabel.Content = result.message;
                                ErrorLabel.Visibility = Visibility.Visible;
                            });
                        }).Start();
                    }
                }).Wait();
            }
        }
    }
}
