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
        // 主数据源
        private ObservableCollection<VaultItem> AllItems =
            new ObservableCollection<VaultItem>();

        // 当前显示
        private ObservableCollection<VaultItem> ViewItems =
            new ObservableCollection<VaultItem>();

        // 崩溃快照文件（与 data.enc 同目录）
        private readonly string CrashPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.enc");

        public MainWindow()
        {
            InitializeComponent();

            VaultGrid.ItemsSource = ViewItems;

            Loaded += (_, _) => StartupLoad();
            Closing += (_, _) => NormalExitSave();
        }

        // ================= 启动加载（含崩溃恢复） =================
        private void StartupLoad()
        {
            // 1️⃣ 检测崩溃文件
            if (File.Exists(CrashPath))
            {
                var result = MessageBox.Show(
                    "检测到上次异常退出。\n是否恢复未保存的数据？",
                    "崩溃恢复",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    LoadFromCrash();
                    return;
                }

                // 放弃恢复
                File.Delete(CrashPath);
            }

            // 正常加载
            LoadFromMainStore();
        }

        private void LoadFromMainStore()
        {
            AllItems.Clear();
            ViewItems.Clear();

            foreach (var v in DataStore.Load())
            {
                AllItems.Add(v);
                ViewItems.Add(v);
            }
        }

        private void LoadFromCrash()
        {
            try
            {
                AllItems.Clear();
                ViewItems.Clear();

                var encrypted = File.ReadAllBytes(CrashPath);
                var json = CryptoService.Decrypt(encrypted);
                var list = JsonSerializer.Deserialize<VaultItem[]>(json);

                if (list != null)
                {
                    foreach (var v in list)
                    {
                        AllItems.Add(v);
                        ViewItems.Add(v);
                    }
                }
            }
            catch
            {
                MessageBox.Show("崩溃恢复失败，数据已损坏。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ================= 正常退出保存 =================
        private void NormalExitSave()
        {
            SaveMainData();

            // 正常退出 → 删除崩溃快照
            if (File.Exists(CrashPath))
                File.Delete(CrashPath);
        }

        // ================= 主保存 =================
        private void SaveMainData()
        {
            DataStore.Save(AllItems);
        }

        // ================= 崩溃快照（每次修改都写） =================
        private void SaveCrashSnapshot()
        {
            try
            {
                var json = JsonSerializer.Serialize(AllItems);
                var encrypted = CryptoService.Encrypt(json);
                File.WriteAllBytes(CrashPath, encrypted);
            }
            catch
            {
                // 崩溃快照失败不影响主流程
            }
        }

        // ================= 新增一行 =================
        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            var item = new VaultItem();
            AllItems.Add(item);
            RefreshView();
            SaveCrashSnapshot();

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

            var realItem = AllItems.First(x => x == viewItem);
            string col = VaultGrid.CurrentCell.Column.Header.ToString();
            string text = Clipboard.GetText();

            if (col == "网站")
            {
                if (!TrySetUrl(realItem, text))
                    return;
            }
            else if (col == "名称") realItem.Name = text;
            else if (col == "账号") realItem.Account = text;
            else if (col == "密码") realItem.Password = text;
            else if (col == "备注") realItem.Remark = text;

            RefreshView();
            SaveCrashSnapshot();
        }

        // ================= 编辑完成（网址校验） =================
        private void VaultGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header.ToString() != "网站")
                return;

            if (e.Row.Item is not VaultItem viewItem)
                return;

            var realItem = AllItems.First(x => x == viewItem);
            var tb = e.EditingElement as TextBox;
            if (tb == null)
                return;

            if (!TrySetUrl(realItem, tb.Text))
            {
                e.Cancel = true;
                return;
            }

            RefreshView();
            SaveCrashSnapshot();
        }

        // ================= 统一网址校验 =================
        private bool TrySetUrl(VaultItem current, string newUrl)
        {
            string normalized = NormalizeUrl(newUrl);
            if (string.IsNullOrEmpty(normalized))
                return true;

            var dup = AllItems.FirstOrDefault(x =>
                x != current && NormalizeUrl(x.Url) == normalized);

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

        // ================= 导入 =================
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
            SaveCrashSnapshot();
        }

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
