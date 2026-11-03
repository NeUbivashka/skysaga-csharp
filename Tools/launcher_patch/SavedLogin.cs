using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace LauncherSavedLogin
{
    public static class SavedLogin
    {
        private const string FolderName = "SkySagaLauncher";
        private const string FileName = "login.dat";
        private const char Separator = '\n';
        private const int CheckBoxColumn = 2;
        private const int CheckBoxRow = 0;
        private const double CheckBoxWidth = 104;
        private const double CheckBoxFontSize = 16;
        private const double PanelShiftLeft = 50;

        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SkySagaLauncher.SavedLogin.v1");

        private static CheckBox _remember;

        private static string FilePath
        {
            get
            {
                return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName), FileName);
            }
        }

        public static void Attach(Grid panel, TextBox username, PasswordBox password)
        {
            try
            {
                _remember = new CheckBox
                {
                    Content = "Remember",
                    ContentStringFormat = "{0}:",
                    ToolTip = "Remember the password, encrypted for this Windows user",
                    FontSize = CheckBoxFontSize,
                    Width = CheckBoxWidth,
                    Margin = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    TabIndex = 4
                };
                Grid.SetColumn(_remember, CheckBoxColumn);
                Grid.SetRow(_remember, CheckBoxRow);
                _remember.Unchecked += delegate { Delete(); };
                panel.Children.Add(_remember);
                panel.Margin = new Thickness(panel.Margin.Left - PanelShiftLeft, panel.Margin.Top, panel.Margin.Right, panel.Margin.Bottom);

                string savedUsername;
                string savedPassword;

                if (Load(out savedUsername, out savedPassword))
                {
                    username.Text = savedUsername;
                    password.Password = savedPassword;
                    _remember.IsChecked = true;
                }
            }
            catch (Exception)
            {
            }
        }

        public static void LoginFinished(bool success, TextBox username, PasswordBox password)
        {
            try
            {
                if (!success || _remember == null)
                    return;

                if (_remember.IsChecked == true)
                    Save(username.Text, password.Password);
                else
                    Delete();
            }
            catch (Exception)
            {
            }
        }

        private static bool Load(out string username, out string password)
        {
            username = null;
            password = null;

            if (!File.Exists(FilePath))
                return false;

            try
            {
                var data = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser);
                var text = Encoding.UTF8.GetString(data);
                var split = text.IndexOf(Separator);

                if (split < 0)
                    return false;

                username = text.Substring(0, split);
                password = text.Substring(split + 1);
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        private static void Save(string username, string password)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var data = Encoding.UTF8.GetBytes(username + Separator + password);
            File.WriteAllBytes(FilePath, ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser));
        }

        private static void Delete()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (IOException)
            {
            }
        }
    }
}
