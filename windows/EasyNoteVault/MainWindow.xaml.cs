using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EasyNoteVault
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<VaultItem> Items { get; } = new();

        public MainWindow()
        {
            InitializeComponent();

            VaultGrid.ItemsSource = Items;

            // 单击复制
            VaultGrid.PreviewMouseLeftButtonUp += VaultGrid_PreviewMouseLeftButtonUp;

            // 编辑完成
            VaultGrid.CellEditEnding += VaultGrid_CellEditEnding;

            // 窗口显示后再加载数据（关键，避免启动秒退）
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataSafeAsync();
        }

        // =============================
        // 安全加载数据（不会崩 UI）
        // =============================
        private async Task LoadDataSafeAsync()
        {
            try
            {
                var data = await Task.Run(() => DataStore.Load());

                Items.Clear();
                foreach (var item in data)
                    Items.Add(item);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "数据加载失败：\n" + ex.Message,
                    "EasyNoteVault",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =============================
        // 编辑完成
        // =============================
        private void VaultGrid_CellEditEnding(
            object? sender,
            DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header?.ToString() == "网站" &&
                e.Row.Item is VaultItem item)
            {
                CheckDuplicateUrl(item);
            }

            // 延迟保存，确保值已提交
            Dispatcher.InvokeAsync(() => DataStore.Save(Items));
        }

        // =============================
        // 单击复制
        // =============================
        private void VaultGrid_PreviewMouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.OriginalSource is TextBlock tb &&
                !string.IsNullOrWhiteSpace(tb.Text))
            {
                Clipboard.SetText(tb.Text);
            }
        }

        // =============================
        // 网址重复检测
        // =============================
        private void CheckDuplicateUrl(VaultItem current)
        {
            var currentUrl = NormalizeUrl(current.Url);
            if (string.IsNullOrEmpty(currentUrl))
                return;

            var duplicates = Items
                .Select((item, index) => new { item, index })
                .Where(x =>
                    x.item != current &&
                    NormalizeUrl(x.item.Url) == currentUrl)
                .ToList();

            if (duplicates.Any())
            {
                var msg = string.Join("\n",
                    duplicates.Select(d =>
                        $"第 {d.index + 1} 行（账号：{d.item.Account}）"));

                MessageBox.Show(
                    $"网址重复：\n{current.Url}\n\n已存在于：\n{msg}",
                    "网址重复提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static string NormalizeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "";

            url = url.Trim().ToLower();
            if (url.EndsWith("/"))
                url = url.TrimEnd('/');

            return url;
        }
    }

    // =============================
    // 数据模型
    // =============================
    public class VaultItem
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string Account { get; set; } = "";
        public string Password { get; set; } = "";
        public string Remark { get; set; } = "";
    }
}
