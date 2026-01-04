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
        // 真正的数据源
        private ObservableCollection<VaultItem> AllItems =
            new ObservableCollection<VaultItem>();

        // 当前显示数据
        private ObservableCollection<VaultItem> ViewItems =
            new ObservableCollection<VaultItem>();

        public MainWindow()
        {
            InitializeComponent();

            VaultGrid.ItemsSource = ViewItems;

            Loaded += (_, _) => LoadData();
            Closing += (_, _) => SaveData();

            // 明确注册，防止再丢
            VaultGrid.PreviewMouseLeftButtonUp += VaultGrid_PreviewMouseLeftButtonUp;
            VaultGrid.CellEditEnding += VaultGrid_CellEditEnding;
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
            RefreshView();
            SaveData();

            VaultGrid.SelectedItem = item;
            VaultGrid.ScrollIntoView(item);
        }

        // ================= 搜索 =================
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshView();
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

        // ================= 右键粘贴 =================
        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!Clipboard.ContainsText())
                return;

            if (VaultGrid.CurrentCell.Item is not VaultItem viewItem)
                return;

            var item = AllItems.FirstOrDefault(x => x == viewItem);
            if (item == null)
                return;

            string col = VaultGrid.CurrentCell.Column.Header.ToString();
            string text = Clipboard.GetText();

            if (col == "网站")
            {
                if (!TrySetUrl(item, text))
                    return;
            }
            else if (col == "名称") item.Name = text;
            else if (col == "账号") item.Account = text;
            else if (col == "密码") item.Password = text;
            else if (col == "备注") item.Remark = text;

            RefreshView();
            SaveData();
        }

        // ================= 重复网址（禁止 + 定位） =================
        private void VaultGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header.ToString() != "网站")
                return;

            if (e.Row.Item is not VaultItem viewItem)
                return;

            var item = AllItems.FirstOrDefault(x => x == viewItem);
            if (item == null)
                return;

            var tb = e.EditingElement as TextBox;
            if (tb == null)
                return;

            if (!TrySetUrl(item, tb.Text))
            {
                e.Cancel = true;
            }
            else
            {
                RefreshView();
                SaveData();
            }
        }

        // ================= 🔥 导入（XAML 需要） =================
        private void Import_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|JSON 文件 (*.json)|*.json"
            };

            if (dlg.ShowDialog() != true)
                return;

            string ext = Path.GetExtension(dlg.FileName).ToLower();
            if (ext == ".txt") ImportTxt(dlg.FileName);
            else if (ext == ".json") ImportJson(dlg.FileName);

            RefreshView();
            SaveData();
        }

        // ================= 🔥 导出（XAML 需要） =================
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            string fileName = DateTime.Now.ToString("yyyyMMddHH") + ".txt";

            SaveFileDialog dlg = new SaveFileDialog
            {
                FileName = fileName,
                Filter = "文本文件 (*.txt)|*.txt"
            };

            if (dlg.ShowDialog() != true)
                return;

            var sb = new StringBuilder();
            sb.AppendLine("名称  网站  账号  密码  备注");

            foreach (var v in AllItems)
            {
                sb.AppendLine(
                    $"{v.Name}  {v.Url}  {v.Account}  {v.Password}  {v.Remark}");
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        }

        // ================= 导入实现 =================
        private void ImportTxt(string path)
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8);

            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5)
                    continue;

                var item = new VaultItem
                {
                    Name = parts[0],
                    Account = parts[2],
                    Password = parts[3],
                    Remark = parts[4]
                };

                if (TrySetUrl(item, parts[1]))
                    AllItems.Add(item);
            }
        }

        private void ImportJson(string path)
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var list = JsonSerializer.Deserialize<VaultItem[]>(json);
            if (list == null)
                return;

            foreach (var item in list)
            {
                if (TrySetUrl(item, item.Url))
                    AllItems.Add(item);
            }
        }

        // ================= 统一网址校验 =================
        private bool TrySetUrl(VaultItem current, string newUrl)
        {
            string norm = NormalizeUrl(newUrl);
            if (string.IsNullOrEmpty(norm))
                return true;

            var dup = AllItems.FirstOrDefault(x =>
                x != current && NormalizeUrl(x.Url) == norm);

            if (dup != null)
            {
                MessageBox.Show(
                    $"该网站已存在，不能重复添加：\n{dup.Url}",
                    "重复网址",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                VaultGrid.SelectedItem = dup;
                VaultGrid.ScrollIntoView(dup);
                return false;
            }

            current.Url = newUrl;
            return true;
        }

        // ================= 刷新视图 =================
        private void RefreshView()
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

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "";

            url = url.Trim().ToLower();
            if (url.EndsWith("/"))
                url = url.TrimEnd('/');

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
