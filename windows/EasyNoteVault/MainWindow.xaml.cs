using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EasyNoteVault
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<VaultItem> Items { get; } =
            new ObservableCollection<VaultItem>();

        public MainWindow()
        {
            InitializeComponent();
            VaultGrid.ItemsSource = Items;

            // 示例数据
            Items.Add(new VaultItem
            {
                Name = "示例",
                Url = "https://example.com",
                Account = "test@example.com",
                Password = "123456",
                Remark = "这是示例数据"
            });

            VaultGrid.PreviewMouseLeftButtonUp += VaultGrid_PreviewMouseLeftButtonUp;
            VaultGrid.CellEditEnding += VaultGrid_CellEditEnding;
        }

        // ================= 新增行 =================
        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            var item = new VaultItem();
            Items.Add(item);
            VaultGrid.SelectedItem = item;
            VaultGrid.ScrollIntoView(item);
        }

        // ================= 删除行 =================
        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var item = btn?.Tag as VaultItem;
            if (item == null) return;

            // 显示删除确认对话框
            string itemName = string.IsNullOrWhiteSpace(item.Name) ? "未命名项目" : item.Name;
            var result = MessageBox.Show(
                $"确定要删除 「{itemName}」 吗？\n\n此操作不可撤销。",
                "删除确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result == MessageBoxResult.Yes)
            {
                Items.Remove(item);
            }
        }

        // ================= 密码可见性切换 =================
        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var item = btn?.Tag as VaultItem;
            if (item == null) return;

            // 切换密码可见状态
            item.IsPasswordVisible = !item.IsPasswordVisible;
        }

        // ================= 单击复制 =================
        private void VaultGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // NOTE: 只在 TextBlock 上触发复制，避免误触按钮
            if (e.OriginalSource is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
            {
                // 如果点击的是显示的密码，复制真实密码
                var item = VaultGrid.CurrentItem as VaultItem;
                string textToCopy = tb.Text;
                
                // 如果是密码遮罩，复制真实密码
                if (tb.Text == "••••••" && item != null)
                {
                    textToCopy = item.Password ?? "";
                }

                if (!string.IsNullOrEmpty(textToCopy))
                {
                    Clipboard.SetText(textToCopy);
                    // 使用更友好的提示
                    ShowToast("已复制到剪贴板");
                }
            }
        }

        // ================= 友好提示（替代 MessageBox） =================
        private void ShowToast(string message)
        {
            // NOTE: 简单实现，未来可替换为自定义 Toast 控件
            MessageBox.Show(message, "EasyNoteVault",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ================= 右键粘贴 =================
        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!Clipboard.ContainsText()) return;
            if (VaultGrid.CurrentCell.Item == null ||
                VaultGrid.CurrentCell.Column == null) return;

            string text = Clipboard.GetText();
            VaultGrid.BeginEdit();

            var item = VaultGrid.CurrentCell.Item as VaultItem;
            if (item == null) return;

            string col = VaultGrid.CurrentCell.Column.Header.ToString();
            if (col == "名称") item.Name = text;
            else if (col == "网站") item.Url = text;
            else if (col == "账号") item.Account = text;
            else if (col == "密码") item.Password = text;
            else if (col == "备注") item.Remark = text;

            VaultGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            VaultGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        // ================= 重复检测 =================
        private void VaultGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header.ToString() != "网站") return;

            var current = e.Row.Item as VaultItem;
            if (current == null) return;

            string url = NormalizeUrl(current.Url);
            if (string.IsNullOrEmpty(url)) return;

            var dup = Items
                .Select((x, i) => new { x, i })
                .Where(x => x.x != current && NormalizeUrl(x.x.Url) == url)
                .ToList();

            if (dup.Count > 0)
            {
                MessageBox.Show(
                    $"网址重复：{current.Url}\n已存在于第 {dup[0].i + 1} 行",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // ================= 导出（双空格分隔） =================
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            string fileName = DateTime.Now.ToString("yyyyMMddHHmm") + ".txt";

            SaveFileDialog dlg = new SaveFileDialog
            {
                FileName = fileName,
                Filter = "文本文件 (*.txt)|*.txt"
            };

            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();

            // 表头（双空格）
            sb.AppendLine("名称  网站  账号  密码  备注");

            foreach (var item in Items)
            {
                sb.AppendLine(
                    $"{item.Name}  {item.Url}  {item.Account}  {item.Password}  {item.Remark}");
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            ShowToast($"已导出到 {dlg.FileName}");
        }

        // ================= 导入（双空格解析） =================
        private void Import_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt"
            };

            if (dlg.ShowDialog() != true) return;

            var lines = File.ReadAllLines(dlg.FileName, Encoding.UTF8);
            int importedCount = 0;

            foreach (var line in lines.Skip(1)) // 跳过表头
            {
                // 用「两个及以上空格」切分
                var parts = line
                    .Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 5) continue;

                Items.Add(new VaultItem
                {
                    Name = parts[0],
                    Url = parts[1],
                    Account = parts[2],
                    Password = parts[3],
                    Remark = parts[4]
                });
                importedCount++;
            }

            ShowToast($"成功导入 {importedCount} 条记录");
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            url = url.Trim().ToLower();
            if (url.EndsWith("/")) url = url.TrimEnd('/');
            return url;
        }
    }

    /// <summary>
    /// 保险库条目数据模型
    /// 实现 INotifyPropertyChanged 以支持 UI 动态更新
    /// </summary>
    public class VaultItem : INotifyPropertyChanged
    {
        private string _name;
        private string _url;
        private string _account;
        private string _password;
        private string _remark;
        private bool _isPasswordVisible = false;

        public event PropertyChangedEventHandler PropertyChanged;

        // NOTE: 用于触发属性变更通知
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Url
        {
            get => _url;
            set { _url = value; OnPropertyChanged(); }
        }

        public string Account
        {
            get => _account;
            set { _account = value; OnPropertyChanged(); }
        }

        public string Password
        {
            get => _password;
            set 
            { 
                _password = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(DisplayPassword)); 
            }
        }

        public string Remark
        {
            get => _remark;
            set { _remark = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 密码是否可见
        /// </summary>
        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set
            {
                _isPasswordVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayPassword));
                OnPropertyChanged(nameof(EyeIcon));
            }
        }

        /// <summary>
        /// 显示的密码（根据可见性状态返回真实密码或遮罩）
        /// </summary>
        public string DisplayPassword => IsPasswordVisible ? Password : "••••••";

        /// <summary>
        /// 眼睛图标（根据可见性状态切换）
        /// </summary>
        public string EyeIcon => IsPasswordVisible ? "🙈" : "👁";
    }
}
