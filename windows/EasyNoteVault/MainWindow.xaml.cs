#nullable enable
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
using System.Windows.Media;
using System.Windows.Threading;

namespace EasyNoteVault
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<VaultItem> AllItems = new ObservableCollection<VaultItem>();
        private ObservableCollection<VaultItem> ViewItems = new ObservableCollection<VaultItem>();

        public MainWindow()
        {
            InitializeComponent();

            VaultGrid.ItemsSource = ViewItems;

            Loaded += (_, _) => LoadData();
            Closing += (_, _) => { ForceCommitGridEdits(); SaveData(); };

            // 事件：XAML 里也绑了，这里不重复绑（避免重复触发）
            // VaultGrid.PreviewMouseLeftButtonUp += VaultGrid_PreviewMouseLeftButtonUp;
            // VaultGrid.CellEditEnding += VaultGrid_CellEditEnding;
        }

        // ================= 工具：强制提交 DataGrid 编辑 =================
        private void ForceCommitGridEdits()
        {
            try
            {
                VaultGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                VaultGrid.CommitEdit(DataGridEditingUnit.Row, true);
            }
            catch { }
        }

        // ================= 加载 / 保存 =================
        private void LoadData()
        {
            AllItems.Clear();
            ViewItems.Clear();

            foreach (var v in DataStore.Load())
                AllItems.Add(v);

            RefreshView();
        }

        private void SaveData()
        {
            ForceCommitGridEdits();
            DataStore.Save(AllItems);
        }

        // ================= 搜索 =================
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshView();
        }

        // ================= 左键复制 =================
        private void VaultGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
            {
                Clipboard.SetText(tb.Text);
                MessageBox.Show("已复制", "EasyNoteVault",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ================= ✅ 右键菜单打开前：定位到你点的“单元格” =================
        // 关键：SelectionUnit=Cell 时，不能 SelectedItem（选行），否则会抛你截图的异常。
        private void VaultGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            try
            {
                var dep = e.OriginalSource as DependencyObject;
                if (dep == null) return;

                var cell = FindVisualParent<DataGridCell>(dep);
                var row = FindVisualParent<DataGridRow>(dep);

                // 点在表头/空白区域/滚动条：没有 cell/row，直接放过
                if (cell == null || row == null) return;

                SetCurrentCellOnly(row.Item, cell.Column);
            }
            catch
            {
                // 永不崩
            }
        }

        private void SetCurrentCellOnly(object rowItem, DataGridColumn column)
        {
            VaultGrid.CurrentCell = new DataGridCellInfo(rowItem, column);
            VaultGrid.SelectedCells.Clear();
            VaultGrid.SelectedCells.Add(VaultGrid.CurrentCell);
            VaultGrid.ScrollIntoView(rowItem, column);
            VaultGrid.Focus();
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? current = child;
            while (current != null)
            {
                if (current is T typed) return typed;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // ================= ✅ 右键粘贴 =================
        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!Clipboard.ContainsText())
                return;

            try
            {
                VaultGrid.Focus();
                ForceCommitGridEdits();

                var colObj = VaultGrid.CurrentCell.Column;
                if (colObj == null)
                    return;

                string col = colObj.Header?.ToString() ?? "";
                string text = Clipboard.GetText();

                // 当前行：可能是 VaultItem，也可能是新增占位符（NewItemPlaceholder）
                VaultItem item;
                if (VaultGrid.CurrentCell.Item is VaultItem vi)
                {
                    item = vi;
                }
                else
                {
                    // ✅ 点在空表/占位行：自动新建一条
                    item = new VaultItem();
                    AllItems.Add(item);

                    // 搜索中粘贴：先清空搜索，保证新行可见
                    if (!string.IsNullOrWhiteSpace(SearchBox.Text))
                        SearchBox.Text = "";

                    RefreshView();
                    SetCurrentCellOnly(item, colObj);
                }

                // 写入
                if (col == "网站")
                {
                    if (!TrySetUrl(item, text))
                        return; // 重复时已提示+定位
                }
                else if (col == "名称") item.Name = text;
                else if (col == "账号") item.Account = text;
                else if (col == "密码") item.Password = text;
                else if (col == "备注") item.Remark = text;

                ForceCommitGridEdits();
                RefreshView();
                SaveData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"粘贴出错：\n{ex.Message}", "EasyNoteVault",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================= 编辑结束：网站列重复校验 + 自动保存 =================
        private void VaultGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is not VaultItem item)
                return;

            string col = e.Column.Header?.ToString() ?? "";

            if (col == "网站")
            {
                if (e.EditingElement is TextBox tb)
                {
                    if (!TrySetUrl(item, tb.Text))
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ForceCommitGridEdits();
                RefreshView();
                SaveData();
            }), DispatcherPriority.Background);
        }

        // ================= 导入 / 导出 =================
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

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            ForceCommitGridEdits();

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
                sb.AppendLine($"{v.Name}  {v.Url}  {v.Account}  {v.Password}  {v.Remark}");
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
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

        // ================= 重复网址：提示 + 拒绝 + 定位到已有项的网站单元格 =================
        private DataGridColumn? GetColumnByHeader(string header)
        {
            return VaultGrid.Columns.FirstOrDefault(c =>
                string.Equals(c.Header?.ToString(), header, StringComparison.Ordinal));
        }

        private void LocateItemAndFocusCell(VaultItem item, string columnHeader)
        {
            if (!ViewItems.Contains(item))
            {
                SearchBox.Text = "";
                RefreshView();
            }

            var col = GetColumnByHeader(columnHeader);
            if (col == null) return;

            SetCurrentCellOnly(item, col);
        }

        private bool TrySetUrl(VaultItem current, string newUrl)
        {
            string norm = NormalizeUrl(newUrl);
            if (string.IsNullOrEmpty(norm))
            {
                current.Url = newUrl ?? "";
                return true;
            }

            var dup = AllItems.FirstOrDefault(x =>
                x != current && NormalizeUrl(x.Url) == norm);

            if (dup != null)
            {
                MessageBox.Show(
                    $"该网站已存在，不能重复添加：\n{dup.Url}",
                    "重复网址",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                LocateItemAndFocusCell(dup, "网站");
                return false;
            }

            current.Url = newUrl ?? "";
            return true;
        }

        // ================= 刷新视图 =================
        private void RefreshView()
        {
            string key = (SearchBox.Text ?? "").Trim().ToLower();
            ViewItems.Clear();

            foreach (var v in AllItems)
            {
                if (string.IsNullOrEmpty(key) ||
                    (v.Name ?? "").ToLower().Contains(key) ||
                    (v.Url ?? "").ToLower().Contains(key) ||
                    (v.Account ?? "").ToLower().Contains(key) ||
                    (v.Remark ?? "").ToLower().Contains(key))
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
