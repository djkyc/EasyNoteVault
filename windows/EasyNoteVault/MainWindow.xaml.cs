using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace EasyNoteVault
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<VaultItem> Items =
            new ObservableCollection<VaultItem>();

        private ICollectionView View;

        public MainWindow()
        {
            InitializeComponent();

            // 加载数据（延迟、安全）
            foreach (var item in DataStore.Load())
                Items.Add(item);

            View = CollectionViewSource.GetDefaultView(Items);
            VaultGrid.ItemsSource = View;

            VaultGrid.PreviewMouseLeftButtonUp += VaultGrid_PreviewMouseLeftButtonUp;
            VaultGrid.CellEditEnding += VaultGrid_CellEditEnding;

            Closing += (_, _) => Save();
        }

        // ================= 自动保存 =================
        private void Save()
        {
            DataStore.Save(Items);
        }

        // ================= 新增 =================
        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            var item = new VaultItem();
            Items.Add(item);
            Save();
        }

        // ================= 搜索 =================
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = SearchBox.Text.Trim().ToLower();

            View.Filter = obj =>
            {
                if (string.IsNullOrEmpty(text))
                    return true;

                var v = obj as VaultItem;
                return v.Name.ToLower().Contains(text) ||
                       v.Url.ToLower().Contains(text) ||
                       v.Account.ToLower().Contains(text) ||
                       v.Remark.ToLower().Contains(text);
            };
        }

        // ================= 复制 =================
        private void VaultGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is TextBlock tb &&
                !string.IsNullOrWhiteSpace(tb.Text))
            {
                Clipboard.SetText(tb.Text);
                MessageBox.Show("已复制", "EasyNoteVault",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ================= 粘贴 =================
        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!Clipboard.ContainsText() ||
                VaultGrid.CurrentCell.Item == null)
                return;

            VaultGrid.BeginEdit();

            var item = VaultGrid.CurrentCell.Item as VaultItem;
            var col = VaultGrid.CurrentCell.Column.Header.ToString();
            var text = Clipboard.GetText();

            if (col == "名称") item.Name = text;
            else if (col == "网站") item.Url = text;
            else if (col == "账号") item.Account = text;
            else if (col == "密码") item.Password = text;
            else if (col == "备注") item.Remark = text;

            VaultGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            VaultGrid.CommitEdit(DataGridEditingUnit.Row, true);

            Save();
        }

        // ================= 重复检测 =================
        private void VaultGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Save();
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
