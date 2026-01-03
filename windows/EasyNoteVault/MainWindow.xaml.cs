public MainWindow()
{
    InitializeComponent();

    // 等窗口真正显示后再加载数据
    Loaded += MainWindow_Loaded;
}

private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
{
    try
    {
        await Task.Run(() =>
        {
            var loaded = DataStore.Load();

            Dispatcher.Invoke(() =>
            {
                Items.Clear();
                foreach (var item in loaded)
                    Items.Add(item);
            });
        });
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
