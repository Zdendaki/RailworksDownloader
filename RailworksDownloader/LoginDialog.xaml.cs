using ModernWpf.Controls;
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
        private Action Callback { get; set; }
        private Uri ApiUrl { get; set; }

        public LoginDialog(Uri apiUrl, Action callback)
        {
            InitializeComponent();
            ApiUrl = apiUrl;
            Callback = callback;
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
                        App.Settings.Username = login.Trim();
                        App.Settings.Password = Utils.PasswordEncryptor.Encrypt(pass, login.Trim());
                        App.Settings.Save();
                        App.Token = result.content.token;
                        Callback();
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

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(App.Token) && !MainWindow.ReportedDLC && Callback.Method.Name == "ReportDLC")
                Callback();
        }
    }
}
