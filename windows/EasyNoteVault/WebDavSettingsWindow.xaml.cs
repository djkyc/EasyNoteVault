#nullable enable
using System;
using System.Windows;

namespace EasyNoteVault
{
    public partial class WebDavSettingsWindow : Window
    {
        private WebDavSettings _settings;

        public WebDavSettingsWindow(WebDavSettings settings)
        {
            InitializeComponent();
            _settings = settings;

            EnableBox.IsChecked = _settings.Enabled;

            ProviderBox.SelectedIndex = _settings.Provider == WebDavProvider.Jianguoyun ? 0 : 1;

            BaseUrlBox.Text = _settings.BaseUrl;
            FolderBox.Text = _settings.RemoteFolder;
            FileBox.Text = _settings.RemoteFileName;

            UserBox.Text = _settings.Username;
            PassBox.Password = WebDavSettingsStore.GetPassword(_settings);

            ApplyProviderTemplate();
        }

        private void ProviderBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ApplyProviderTemplate();
        }

        private void ApplyProviderTemplate()
        {
            if (ProviderBox.SelectedIndex == 0)
            {
                BaseUrlBox.Text = "https://dav.jianguoyun.com/dav/";
            }
        }

        private async void Test_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var s = BuildSettingsFromUI();

                string localPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.enc");
                string remoteUrl = WebDavUrlBuilder.BuildRemoteFileUrl(s);

                using var svc = new WebDavSyncService(
                    s.Username,
                    PassBox.Password,
                    () => localPath,
                    () => remoteUrl);

                svc.Enabled = true;

                bool ok = await svc.TestAsync();

                MessageBox.Show(ok ? "连接正常" : "连接失败（请检查 URL/账号/应用密码/目录）",
                    "WebDAV 测试",
                    MessageBoxButton.OK,
                    ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "测试失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var s = BuildSettingsFromUI();
                WebDavSettingsStore.SetPassword(s, PassBox.Password);
                WebDavSettingsStore.Save(s);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private WebDavSettings BuildSettingsFromUI()
        {
            var s = _settings;

            s.Enabled = EnableBox.IsChecked == true;
            s.Provider = ProviderBox.SelectedIndex == 0 ? WebDavProvider.Jianguoyun : WebDavProvider.Custom;

            s.BaseUrl = (BaseUrlBox.Text ?? "").Trim();
            s.RemoteFolder = (FolderBox.Text ?? "").Trim();
            s.RemoteFileName = (FileBox.Text ?? "").Trim();

            s.Username = (UserBox.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(s.BaseUrl))
                throw new Exception("BaseUrl 不能为空");
            if (string.IsNullOrWhiteSpace(s.RemoteFolder))
                throw new Exception("远端目录不能为空");
            if (string.IsNullOrWhiteSpace(s.RemoteFileName))
                throw new Exception("文件名不能为空");
            if (string.IsNullOrWhiteSpace(s.Username))
                throw new Exception("账号不能为空");
            if (string.IsNullOrWhiteSpace(PassBox.Password))
                throw new Exception("密码不能为空（坚果云请使用应用密码）");

            return s;
        }
    }
}
