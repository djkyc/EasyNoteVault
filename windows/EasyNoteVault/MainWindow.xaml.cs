using System.Collections.ObjectModel;
using System.Linq;
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

            // 示例数据（可删）
            Items.Add(new VaultItem
            {
                Name = "示例",
                Url = "https://example.com",
                Account = "test@example.com",
                Password = "123456",
                Remark = "这是示例数据"
            });

            // 单击复制
            VaultGrid.PreviewMouseLeftButtonUp += VaultGrid_PreviewMouseLeftButtonUp;

            // 编辑完成检测重复
            VaultGrid.CellEditEnding += VaultGrid_CellEditEnding;
        }

        // =============================
        // 单击复制（网站 / 账号 / 密码）
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
        // 编辑完成：检测网址重复
        // =============================
        private void VaultGrid_CellEditEnding(
            object? sender,
            DataGridCellEditEndingEventArgs e)
        {
            // 只检测“网站”列
            if (e.Column.Header?.ToString() != "网站")
                return;

            if (e.Row.Item is not VaultItem current)
                return;

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

        // =============================
        // 网址标准化
        // =============================
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
    // 数据模型（纯 UI）
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
