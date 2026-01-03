using System.Collections.ObjectModel;
using System.Windows;

namespace EasyNoteVault
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<VaultItem> Items { get; } = new();

        public MainWindow()
        {
            InitializeComponent();

            // 绑定数据源（纯 UI，不做任何逻辑）
            VaultGrid.ItemsSource = Items;

            // 示例数据（方便你一眼看到效果，可随时删）
            Items.Add(new VaultItem
            {
                Name = "示例",
                Url = "https://example.com",
                Account = "test@example.com",
                Password = "123456",
                Remark = "这是示例数据"
            });
        }
    }

    // 纯数据模型（无逻辑）
    public class VaultItem
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string Account { get; set; } = "";
        public string Password { get; set; } = "";
        public string Remark { get; set; } = "";
    }
}
