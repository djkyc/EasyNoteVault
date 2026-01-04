using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EasyNoteVault
{
    public partial class MainWindow : Window
    {
        // 全量数据（真实源）
        private ObservableCollection<VaultItem> AllItems =
            new ObservableCollection<VaultItem>();

        // 当前显示数据（搜索结果）
        private ObservableCollection<VaultItem> ViewItems =
            new ObservableCollection<VaultItem>();

        public MainWindow()
        {
            InitializeComponent();

            VaultGrid.ItemsSource = ViewItems;

            Loaded += (_, _) => LoadData();
            Closing += (_, _) => SaveData();
        }

        // ================= 加载 / 保存 =================
        private void LoadData()
        {
            AllItems.Clear();
            ViewItems.Clear();

            foreach (var v in DataStore.Load())
            {
                AllItems.Add(v);
                ViewItems.Add(v);
            }
        }

        private void SaveData()
        {
            DataStore.Save(AllItems);
        }

        // ================= 新增一行 =================
        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            var item = new VaultItem();
            AllItems.Add(item);
            ViewItems.Add(item);

            VaultGrid.SelectedItem = item;
            VaultGrid.ScrollIntoView(item);
        }

        // ================= 搜索 =================
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string key = SearchBox.Text.Trim().ToLower();
            ViewItems.Clear();

            foreach (var v in AllItems)
            {
                if (string.IsNullOrEmpty(key) ||
                    v.Name.ToLower().Contains(key) ||
                    v.Url.ToLower().Contains(key) ||
                    v.Account.ToLower().Contains(key) ||
                    v.Remark.ToLower().Contains(key))
                {
                    ViewItems.Add(v);
                }
            }
        }

        // ================= 左键复制 =================
        private void VaultGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is TextBlock tb &&
                !string.IsNullOrWhiteSpace(tb.Text))
            {
                Clipboard.SetText(tb.Text);
                MessageBox.Show("已复制",
                    "EasyNoteVault",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // ================= 右键粘贴（统一校验） =================
        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!Clipboard.ContainsText()) return;
            if (VaultGrid.CurrentCell.Item is not VaultItem item) return;

            string text = Clipboard.GetText();
            string col = VaultGrid.CurrentCell.Column.Header.ToString();

            if (col == "网站")
            {
                if (!TrySetUrl(item, text))
                    return;
            }
            else if (col == "名称") item.Name = text;
            else if (col == "账号") item.Account = text;
            else if (col == "密码") item.Password = text;
            else if (col == "备注") item.Remark = text;
        }

        // ================= 手动编辑完成（统一校验） =================
        private void VaultGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header.ToString() != "网站") return;
            if (e.Row.Item is not VaultItem item) return;

            var tb = e.EditingElement as TextBox;
            if (tb == null) return;

            if (!TrySetUrl(item, tb.Text))
            {
                e.Cancel = true;
            }
        }

        // ================= 导入 TXT / JSON（统一校验） =================
        private void ImportTxt(string path)
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8);

            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;

                var item = new VaultItem
                {
                    Name = parts[0],
                    Account = parts[2],
                    Password = parts[3],
                    Remark = parts[4]
                };

                if (TrySetUrl(item, parts[1]))
                {
                    AllItems.Add(item);
                    ViewItems.Add(item);
                }
            }
        }

        private void ImportJson(string path)
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var list = JsonSerializer.Deserialize<VaultItem[]>(json);
            if (list == null) return;

            foreach (var item in list)
            {
                if (TrySetUrl(item, item.Url))
                {
                    AllItems.Add(item);
                    ViewItems.Add(item);
                }
            }
        }

        // ================= 🔥 统一网站校验（终极） =================
        private bool TrySetUrl(VaultItem current, string newUrl)
        {
            string normalized = NormalizeUrl(newUrl);
            if (string.IsNullOrEmpty(normalized))
                return true;

            var duplicate = AllItems
                .FirstOrDefault(x => x != current &&
                                     NormalizeUrl(x.Url) == normalized);

            if (duplicate != null)
            {
                MessageBox.Show(
                    $"该网站已存在，不能重复添加：\n{duplicate.Url}",
                    "重复网址",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                VaultGrid.SelectedItem = duplicate;
                VaultGrid.ScrollIntoView(duplicate);
                return false;
            }

            current.Url = newUrl;
            return true;
        }

        // ================= 工具 =================
        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            url = url.Trim().ToLower();
            if (url.EndsWith("/")) url = url.TrimEnd('/');
            return url;
        }
    }

    public class VaultItem
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string Account { get; set; } = "";
        public string Password { get; set; } = "";
        public string Remark { get; set; } = "";
    }
}
