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
        private Action CallBack { get; set; }
        private Uri ApiUrl { get; set; }

        public LoginDialog(Uri apiUrl, Action callback)
        {
            InitializeComponent();
            ApiUrl = apiUrl;
            CallBack = callback;
            ShowAsync();
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string login = Username.Text;
            string pass = Password.Password;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(pass))
            {
                args.Cancel = true;
                ErrorLabel.Content = Localization.Strings.LoginMissingField;
                ErrorLabel.Visibility = Visibility.Visible;
            }
            else
            {
                Task.Run(async () =>
                {
                    ObjectResult<LoginContent> result = await WebWrapper.Login(login.Trim(), pass, ApiUrl);
                    if (result != null && Utils.IsSuccessStatusCode(result.code) && result.content?.privileges >= 0)
                    {
                        Settings.Default.Username = login.Trim();
                        Settings.Default.Password = Utils.PasswordEncryptor.Encrypt(pass, login.Trim());
                        Settings.Default.Save();
                        App.Token = result.content.token;
                        CallBack();
                    }
                    else
                    {
                        args.Cancel = true;

                        new Task(() =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (result != null)
                                    ErrorLabel.Content = result.message;
                                else
                                    ErrorLabel.Content = Localization.Strings.UnknownError;
                                ErrorLabel.Visibility = Visibility.Visible;
                            });
                        }).Start();
                    }
                }).Wait();
            }
        }
    }
}
