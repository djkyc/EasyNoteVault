using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EasyNoteVault
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<VaultItem> AllItems = new ObservableCollection<VaultItem>();
        private ObservableCollection<VaultItem> ViewItems = new ObservableCollection<VaultItem>();

        private WebDavSettings _webdavSettings = new WebDavSettings();
        private WebDavSyncService _webdav = null;
        private string _webdavLastDetail = "未启用 WebDAV";

        private DispatcherTimer _toastTimer = null;

        public MainWindow()
        {
            InitializeComponent();
            VaultGrid.ItemsSource = ViewItems;

            Loaded += (_, _) =>
            {
                LoadData();
                LoadWebDavSettingsAndSetup();
            };

            Closing += (_, _) =>
            {
                ForceCommitGridEdits();
                SaveData();
                try { if (_webdav != null) _webdav.Dispose(); } catch { }
            };

            _toastTimer = new DispatcherTimer();
            _toastTimer.Interval = TimeSpan.FromMilliseconds(900);
            _toastTimer.Tick += (_, _) =>
            {
                _toastTimer.Stop();
                FadeOutToast();
            };
        }

        // ================= ✅ Toast：不打断提示 =================
        private void ShowToast(string message)
        {
            try
            {
                ToastText.Text = message;
                ToastBorder.Visibility = Visibility.Visible;
                ToastBorder.Opacity = 1;

                _toastTimer.Stop();
                _toastTimer.Start();
            }
            catch { }
        }

        private void FadeOutToast()
        {
            try
            {
                var anim = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(280),
                    FillBehavior = FillBehavior.Stop
                };

                anim.Completed += (_, _) =>
                {
                    ToastBorder.Opacity = 0;
                    ToastBorder.Visibility = Visibility.Collapsed;
                };

                ToastBorder.BeginAnimation(UIElement.OpacityProperty, anim);
            }
            catch
            {
                ToastBorder.Opacity = 0;
                ToastBorder.Visibility = Visibility.Collapsed;
            }
        }

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
            {
                EnsureCredentials(v);
                AllItems.Add(v);
            }

            RefreshView();
        }

        private void SaveData()
        {
            ForceCommitGridEdits();

            // 保存前：把主表 Account/Password 同步到 Credentials[0]
            foreach (var it in AllItems)
                SyncPrimaryToCredentials(it);

            DataStore.Save(AllItems);

            if (_webdav != null) _webdav.NotifyLocalChanged();
        }

        // ================= 搜索 =================
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshView();
        }

        // ================= ✅ 单击：进入编辑（可直接输入） =================
        private void VaultGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ClickCount != 1) return;

                var dep = e.OriginalSource as DependencyObject;
                if (dep == null) return;

                var cell = FindVisualParent<DataGridCell>(dep);
                if (cell == null) return;

                if (cell.Column == null || cell.IsReadOnly) return;

                if (e.OriginalSource is TextBox || e.OriginalSource is PasswordBox)
                    return;

                var rowItem = cell.DataContext;
                if (rowItem == null) return;

                VaultGrid.CurrentCell = new DataGridCellInfo(rowItem, cell.Column);
                VaultGrid.SelectedCells.Clear();
                VaultGrid.SelectedCells.Add(VaultGrid.CurrentCell);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    VaultGrid.BeginEdit();
                }), DispatcherPriority.Input);
            }
            catch { }
        }

        // ================= ✅ 双击：复制单元格内容（Toast 不打断） =================
        private void VaultGrid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                string text = "";

                if (e.OriginalSource is TextBlock tb)
                    text = tb.Text;

                if (string.IsNullOrWhiteSpace(text) && e.OriginalSource is TextBox tbox)
                    text = tbox.Text;

                if (string.IsNullOrWhiteSpace(text))
                    return;

                Clipboard.SetText(text);
                ShowToast("已复制");
                e.Handled = true;
            }
            catch { }
        }

        // ================= ✅ 右键菜单打开前：只选中单元格（避免 SelectionUnit=Cell 报错/闪退） =================
        private void VaultGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            try
            {
                var dep = e.OriginalSource as DependencyObject;
                if (dep == null) return;

                var cell = FindVisualParent<DataGridCell>(dep);
                var row = FindVisualParent<DataGridRow>(dep);

                if (cell == null || row == null) return;

                SetCurrentCellOnly(row.Item, cell.Column);
            }
            catch { }
        }

        private void SetCurrentCellOnly(object rowItem, DataGridColumn column)
        {
            VaultGrid.CurrentCell = new DataGridCellInfo(rowItem, column);
            VaultGrid.SelectedCells.Clear();
            VaultGrid.SelectedCells.Add(VaultGrid.CurrentCell);
            VaultGrid.ScrollIntoView(rowItem, column);
            VaultGrid.Focus();
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject current = child;
            while (current != null)
            {
                if (current is T typed) return typed;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // ================= 右键粘贴 =================
        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!Clipboard.ContainsText())
                return;

            try
            {
                VaultGrid.Focus();
                ForceCommitGridEdits();

                var colObj = VaultGrid.CurrentCell.Column;
                if (colObj == null) return;

                string col = colObj.Header == null ? "" : colObj.Header.ToString();
                string clip = Clipboard.GetText();

                VaultItem item;
                if (VaultGrid.CurrentCell.Item is VaultItem vi)
                {
                    item = vi;
                }
                else
                {
                    item = new VaultItem();
                    EnsureCredentials(item);
                    AllItems.Add(item);

                    if (!string.IsNullOrWhiteSpace(SearchBox.Text))
                        SearchBox.Text = "";

                    RefreshView();
                    SetCurrentCellOnly(item, colObj);
                }

                if (col == "网站")
                {
                    if (!TrySetUrl(item, clip))
                        return;
                }
                else if (col == "名称") item.Name = clip;
                else if (col == "账号") item.Account = clip;
                else if (col == "密码") item.Password = clip;
                else if (col == "备注") item.Remark = clip;

                ForceCommitGridEdits();

                // 账号/密码改动后同步到 Credentials[0]
                EnsureCredentials(item);
                SyncPrimaryToCredentials(item);

                RefreshView();
                SaveData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"粘贴出错：\n{ex.Message}",
                    "EasyNoteVault", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================= 编辑结束：网站列重复校验 + 保存 =================
        private void VaultGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (!(e.Row.Item is VaultItem item))
                return;

            string col = e.Column.Header == null ? "" : e.Column.Header.ToString();

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

                EnsureCredentials(item);
                SyncPrimaryToCredentials(item);

                RefreshView();
                SaveData();
            }), DispatcherPriority.Background);
        }

        // ================= 网址去重：重复 -> 提示 + 定位 + 拒绝 =================
        private DataGridColumn GetColumnByHeader(string header)
        {
            return VaultGrid.Columns.FirstOrDefault(c =>
                string.Equals(c.Header == null ? "" : c.Header.ToString(), header, StringComparison.Ordinal));
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
                MessageBox.Show($"该网站已存在，不能重复添加：\n{dup.Url}",
                    "重复网址", MessageBoxButton.OK, MessageBoxImage.Warning);

                LocateItemAndFocusCell(dup, "网站");
                return false;
            }

            current.Url = newUrl ?? "";
            return true;
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
                sb.AppendLine($"{v.Name}  {v.Url}  {v.Account}  {v.Password}  {v.Remark}");

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

                EnsureCredentials(item);
                SyncPrimaryToCredentials(item);

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
                EnsureCredentials(item);
                SyncPrimaryToCredentials(item);

                if (TrySetUrl(item, item.Url))
                    AllItems.Add(item);
            }
        }

        // ================= ✅ 多账号密码：弹窗管理 =================
        private void ManageCreds_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ForceCommitGridEdits();

                var btn = sender as Button;
                if (btn == null) return;

                var item = btn.DataContext as VaultItem;
                if (item == null) return;

                EnsureCredentials(item);
                SyncPrimaryToCredentials(item);

                var dlg = new CredentialsManagerWindow(item, this);
                var ok = dlg.ShowDialog();

                if (ok == true)
                {
                    item.Credentials = dlg.Result;

                    // 主表显示第一组
                    SyncCredentialsToPrimary(item);

                    RefreshView();
                    SaveData();
                    ShowToast("已保存多账号");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "管理账号失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnsureCredentials(VaultItem item)
        {
            if (item.Credentials == null)
                item.Credentials = new ObservableCollection<CredentialPair>();

            if (item.Credentials.Count == 0)
            {
                item.Credentials.Add(new CredentialPair
                {
                    Account = item.Account ?? "",
                    Password = item.Password ?? ""
                });
            }
        }

        private void SyncPrimaryToCredentials(VaultItem item)
        {
            EnsureCredentials(item);
            item.Credentials[0].Account = item.Account ?? "";
            item.Credentials[0].Password = item.Password ?? "";
        }

        private void SyncCredentialsToPrimary(VaultItem item)
        {
            EnsureCredentials(item);
            item.Account = item.Credentials[0].Account ?? "";
            item.Password = item.Credentials[0].Password ?? "";
        }

        // ================= WebDAV（保持不变） =================
        private void WebDav_Click(object sender, RoutedEventArgs e)
        {
            var win = new WebDavSettingsWindow(_webdavSettings) { Owner = this };
            if (win.ShowDialog() == true)
            {
                LoadWebDavSettingsAndSetup();
            }
        }

        private void WebDavStatus_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(_webdavLastDetail, "WebDAV 状态",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadWebDavSettingsAndSetup()
        {
            _webdavSettings = WebDavSettingsStore.Load();
            SetupWebDavService();
        }

        private void SetupWebDavService()
        {
            try { if (_webdav != null) _webdav.Dispose(); } catch { }
            _webdav = null;

            if (!_webdavSettings.Enabled)
            {
                _webdavLastDetail = $"[{DateTime.Now:HH:mm:ss}] 未启用 WebDAV";
                WebDavStatusBtn.Background = Brushes.Gray;
                WebDavStatusBtn.ToolTip = _webdavLastDetail;
                return;
            }

            var pass = WebDavSettingsStore.GetPassword(_webdavSettings);
            if (string.IsNullOrWhiteSpace(_webdavSettings.Username) || string.IsNullOrWhiteSpace(pass))
            {
                _webdavLastDetail = $"[{DateTime.Now:HH:mm:ss}] WebDAV 未配置完整（账号/密码为空）";
                WebDavStatusBtn.Background = Brushes.IndianRed;
                WebDavStatusBtn.ToolTip = _webdavLastDetail;
                return;
            }

            string localPath = DataStore.FilePath;
            string remoteUrl = WebDavUrlBuilder.BuildRemoteFileUrl(_webdavSettings);

            _webdav = new WebDavSyncService(
                _webdavSettings.Username,
                pass,
                () => localPath,
                () => remoteUrl);

            _webdav.Enabled = true;

            _webdav.StatusChanged += (state, msg, detail) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _webdavLastDetail = detail;
                    WebDavStatusBtn.ToolTip = detail;

                    if (state == WebDavSyncState.Queued)
                        WebDavStatusBtn.Background = Brushes.Gold;
                    else if (state == WebDavSyncState.Connected || state == WebDavSyncState.Uploaded)
                        WebDavStatusBtn.Background = Brushes.LimeGreen;
                    else if (state == WebDavSyncState.Failed)
                        WebDavStatusBtn.Background = Brushes.IndianRed;
                    else
                        WebDavStatusBtn.Background = Brushes.Gray;
                });
            };

            _ = _webdav.TestAsync();
        }
    }

    // ================= 数据模型：一个网站多个账号密码 =================
    public class VaultItem
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string Account { get; set; } = "";
        public string Password { get; set; } = "";
        public string Remark { get; set; } = "";

        public ObservableCollection<CredentialPair> Credentials { get; set; } = new ObservableCollection<CredentialPair>();
    }

    public class CredentialPair
    {
        public string Account { get; set; } = "";
        public string Password { get; set; } = "";
    }

    // ================= 多账号管理弹窗（不新增文件，写在同文件） =================
    public class CredentialsManagerWindow : Window
    {
        private readonly ObservableCollection<CredentialPair> _working;
        private readonly DataGrid _grid;

        public ObservableCollection<CredentialPair> Result { get; private set; }

        public CredentialsManagerWindow(VaultItem item, Window owner)
        {
            Owner = owner;
            Title = "多账号 / 密码管理";
            Width = 520;
            Height = 380;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            _working = new ObservableCollection<CredentialPair>(
                (item.Credentials ?? new ObservableCollection<CredentialPair>())
                .Select(x => new CredentialPair { Account = x.Account, Password = x.Password })
            );

            if (_working.Count == 0)
                _working.Add(new CredentialPair());

            Result = _working;

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var top = new StackPanel { Orientation = Orientation.Horizontal };
            var addBtn = new Button { Content = "＋新增", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            var delBtn = new Button { Content = "－删除选中", Width = 110 };

            addBtn.Click += (_, __) =>
            {
                _working.Add(new CredentialPair());
                _grid.SelectedIndex = _working.Count - 1;
                _grid.ScrollIntoView(_grid.SelectedItem);
                _grid.Focus();
                _grid.BeginEdit();
            };

            delBtn.Click += (_, __) =>
            {
                if (_grid.SelectedItem is CredentialPair sel)
                {
                    _working.Remove(sel);
                    if (_working.Count == 0)
                        _working.Add(new CredentialPair());
                }
            };

            top.Children.Add(addBtn);
            top.Children.Add(delBtn);
            Grid.SetRow(top, 0);
            root.Children.Add(top);

            _grid = new DataGrid
            {
                ItemsSource = _working,
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                IsReadOnly = false,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                RowHeight = 30,
                Margin = new Thickness(0, 10, 0, 10)
            };

            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = "账号",
                Binding = new System.Windows.Data.Binding("Account"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = "密码",
                Binding = new System.Windows.Data.Binding("Password"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            _grid.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Delete)
                {
                    if (_grid.SelectedItem is CredentialPair sel)
                    {
                        _working.Remove(sel);
                        if (_working.Count == 0)
                            _working.Add(new CredentialPair());
                        e.Handled = true;
                    }
                }
            };

            Grid.SetRow(_grid, 1);
            root.Children.Add(_grid);

            var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = "保存", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "取消", Width = 90 };

            ok.Click += (_, __) =>
            {
                // 去掉尾部完全空行
                for (int i = _working.Count - 1; i >= 0; i--)
                {
                    var c = _working[i];
                    if (string.IsNullOrWhiteSpace(c.Account) && string.IsNullOrWhiteSpace(c.Password))
                        _working.RemoveAt(i);
                    else
                        break;
                }
                if (_working.Count == 0)
                    _working.Add(new CredentialPair());

                DialogResult = true;
                Close();
            };

            cancel.Click += (_, __) =>
            {
                DialogResult = false;
                Close();
            };

            bottom.Children.Add(ok);
            bottom.Children.Add(cancel);
            Grid.SetRow(bottom, 2);
            root.Children.Add(bottom);

            Content = root;
        }
    }

    // ================= DataStore（加密保存到 data.enc） =================
    public static class DataStore
    {
        public static readonly string FilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.enc");

        public static VaultItem[] Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new VaultItem[0];

                var bytes = File.ReadAllBytes(FilePath);

                string json;
                try
                {
                    var raw = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                    json = Encoding.UTF8.GetString(raw);
                }
                catch
                {
                    json = Encoding.UTF8.GetString(bytes);
                }

                var list = JsonSerializer.Deserialize<VaultItem[]>(json);
                return list ?? new VaultItem[0];
            }
            catch
            {
                return new VaultItem[0];
            }
        }

        public static void Save(ObservableCollection<VaultItem> items)
        {
            try
            {
                var json = JsonSerializer.Serialize(items.ToList());
                var raw = Encoding.UTF8.GetBytes(json);

                var enc = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(FilePath, enc);
            }
            catch
            {
                // 不弹窗
            }
        }
    }
}
