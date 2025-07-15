using FormatLog;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MahApps.Metro.Controls;

namespace DemoWPF
{
    /// <summary>
    /// 主窗口类，负责日志写入和控制界面交互。
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        /// <summary>
        /// 记录每个日志写入类型对应的取消令牌源。
        /// </summary>
        private readonly Dictionary<CheckBox, CancellationTokenSource> _writers = new();

        /// <summary>
        /// 刷新日志写入信息的定时器。
        /// </summary>
        private DispatcherTimer _flushInfoTimer;

        /// <summary>
        /// 日志列表视图模型集合，绑定到日志查询结果。
        /// </summary>
        private ObservableCollection<LogViewModel> _logList = new();

        /// <summary>
        /// 下一页日志的游标时间戳。
        /// </summary>
        private long? _nextCursorTick = null;

        /// <summary>
        /// 上一页日志的游标时间戳。
        /// </summary>
        private long? _prevCursorTick = null;

        /// <summary>
        /// 查询参数模型，包含筛选、排序、分页等参数。
        /// </summary>
        private QueryModel _queryModel = new QueryModel();

        /// <summary>
        /// 查询按钮点击事件，重置分页并查询。
        /// </summary>
        /// <param name="sender">事件源对象。</param>
        /// <param name="e">事件参数。</param>
        private void BtnQuery_Click(object sender, RoutedEventArgs e)
        {
            _queryModel = new QueryModel();
            _queryModel.PageSize = 50;
            _queryModel.WithArgs(txtArgs.Text);
            _queryModel.WithCaller(txtCaller.Text);
            _queryModel.WithFormat(txtFormat.Text);
            if (cmbLevel.SelectedIndex > 0)
            {
                _queryModel.WithLevel((LogLevel)(cmbLevel.SelectedIndex - 1));
            }

            // 新增：读取时间范围并设置
            if (dpStart.SelectedDateTime.HasValue && dpEnd.SelectedDateTime.HasValue)
            {
                try
                {
                    // 确保开始时间小于结束时间
                    if (dpStart.SelectedDateTime.Value > dpEnd.SelectedDateTime.Value)
                    {
                        var temp = dpStart.SelectedDateTime.Value;
                        dpStart.SelectedDateTime = dpEnd.SelectedDateTime.Value;
                        dpEnd.SelectedDateTime = temp;
                    }

                    _queryModel.WithTime(dpStart.SelectedDateTime.Value, dpEnd.SelectedDateTime.Value);
                }
                catch { /* 时间解析失败时忽略 */ }
            }

            if (rbOrderAsc.IsChecked == true)
            {
                _queryModel.OrderBy(OrderType.OrderByIdAscending);
            }
            if (rbOrderDesc.IsChecked == true)
            {
                _queryModel.OrderBy(OrderType.OrderByIdDescending);
            }

            QueryLogs(true);
        }

        /// <summary>
        /// 获取当前写入日志等级。
        /// </summary>
        private LogLevel GetSelectedWriteLogLevel()
        {
            // ComboBoxItem顺序: Debug=0, Info=1, Warning=2, Error=3, Critical=4
            switch (cmbWriteLevel.SelectedIndex)
            {
                case 0: return LogLevel.Debug;
                case 1: return LogLevel.Info;
                case 2: return LogLevel.Warning;
                case 3: return LogLevel.Error;
                case 4: return LogLevel.Critical;
                default: return LogLevel.Info;
            }
        }

        /// <summary>
        /// 窗口加载事件处理，初始化后台日志写入和绑定 CheckBox 事件。
        /// </summary>
        /// <param name="sender">事件源对象。</param>
        /// <param name="e">事件参数。</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            FLog.InitLogBackgroundWorker();
            chkSystemInfo.Checked += (s, e2) => StartWriter(chkSystemInfo, WriteSystemInfoAsync);
            chkSystemInfo.Unchecked += (s, e2) => StopWriter(chkSystemInfo);
            chkMultiplication.Checked += (s, e2) => StartWriter(chkMultiplication, WriteMultiplicationAsync);
            chkMultiplication.Unchecked += (s, e2) => StopWriter(chkMultiplication);
            chkDivision.Checked += (s, e2) => StartWriter(chkDivision, WriteDivisionAsync);
            chkDivision.Unchecked += (s, e2) => StopWriter(chkDivision);
            chkRandomString.Checked += (s, e2) => StartWriter(chkRandomString, WriteRandomStringAsync);
            chkRandomString.Unchecked += (s, e2) => StopWriter(chkRandomString);
            chkLongText.Checked += (s, e2) => StartWriter(chkLongText, WriteLongTextAsync);
            chkLongText.Unchecked += (s, e2) => StopWriter(chkLongText);
            chkShortText.Checked += (s, e2) => StartWriter(chkShortText, WriteShortTextAsync);
            chkShortText.Unchecked += (s, e2) => StopWriter(chkShortText);
            chkTimestamp.Checked += (s, e2) => StartWriter(chkTimestamp, WriteTimestampAsync);
            chkTimestamp.Unchecked += (s, e2) => StopWriter(chkTimestamp);
            chkUserInfo.Checked += (s, e2) => StartWriter(chkUserInfo, WriteUserInfoAsync);
            chkUserInfo.Unchecked += (s, e2) => StopWriter(chkUserInfo);
            chkDiskInfo.Checked += (s, e2) => StartWriter(chkDiskInfo, WriteDiskInfoAsync);
            chkDiskInfo.Unchecked += (s, e2) => StopWriter(chkDiskInfo);
            chkNetworkInfo.Checked += (s, e2) => StartWriter(chkNetworkInfo, WriteNetworkInfoAsync);
            chkNetworkInfo.Unchecked += (s, e2) => StopWriter(chkNetworkInfo);

            rbOrderAsc.Checked += (s, e2) => { _queryModel.OrderBy(OrderType.OrderByIdAscending); QueryLogs(true); };
            rbOrderDesc.Checked += (s, e2) => { _queryModel.OrderBy(OrderType.OrderByIdDescending); QueryLogs(true); };
            rbOrderAsc.IsChecked = true; // 默认选中升序

            btnQuery.Click += BtnQuery_Click;
            btnPrevPage.Click += (s, e2) => { _queryModel.WithPrevCursorTick(_prevCursorTick); QueryLogs(false); };
            btnNextPage.Click += (s, e2) => { _queryModel.WithCursorTick(_nextCursorTick); QueryLogs(false); };
            lvLogs.PreviewMouseWheel += (s, e2) =>
            {
                if (e2.Delta > 0 && _prevCursorTick != null)
                {
                    _queryModel.WithPrevCursorTick(_prevCursorTick);
                    QueryLogs(false);
                    e2.Handled = true;
                }
                else if (e2.Delta < 0 && _nextCursorTick != null)
                {
                    _queryModel.WithCursorTick(_nextCursorTick);
                    QueryLogs(false);
                    e2.Handled = true;
                }
            };

            lvLogs.ItemsSource = _logList;

            cmbLevel.SelectedIndex = 0; // 默认全部
        }

        /// <summary>
        /// 查询日志并刷新列表。
        /// </summary>
        /// <param name="resetPage">是否重置分页。</param>
        private async void QueryLogs(bool resetPage)
        {
            btnQuery.IsEnabled = false;
            btnPrevPage.IsEnabled = false;
            btnNextPage.IsEnabled = false;
            var sw = Stopwatch.StartNew();
            try
            {
                if (resetPage)
                {
                    _queryModel.NextCursorTick = null;
                    _queryModel.PrevCursorTick = null;
                }
                UpdateFlushInfo(); // 查询时也刷新写入信息
                var page = await _queryModel.KeysetPaginationAsync();
                sw.Stop();
                txtQueryTime.Text = $"查询耗时: {sw.Elapsed.TotalMilliseconds:F3} ms";
                _logList.Clear();
                foreach (var log in page.Items)
                {
                    _logList.Add(new LogViewModel(log));
                }
                _nextCursorTick = page.NextCursorTick;
                _prevCursorTick = page.PreCursorTick;
                btnPrevPage.IsEnabled = _prevCursorTick != null;
                btnNextPage.IsEnabled = _nextCursorTick != null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnQuery.IsEnabled = true;
            }
        }

        /// <summary>
        /// 启动指定类型的日志写入任务。
        /// </summary>
        /// <param name="checkBox">对应的 CheckBox 控件。</param>
        /// <param name="writerFunc">日志写入方法。</param>
        private void StartWriter(CheckBox checkBox, Func<CancellationToken, Task> writerFunc)
        {
            if (_writers.ContainsKey(checkBox)) return;
            var cts = new CancellationTokenSource();
            _writers[checkBox] = cts;
            _ = writerFunc(cts.Token);
        }

        /// <summary>
        /// 停止指定类型的日志写入任务。
        /// </summary>
        /// <param name="checkBox">对应的 CheckBox 控件。</param>
        private void StopWriter(CheckBox checkBox)
        {
            if (_writers.TryGetValue(checkBox, out var cts))
            {
                cts.Cancel();
                _writers.Remove(checkBox);
            }
        }

        /// <summary>
        /// 更新刷新信息，显示日志写入性能。
        /// </summary>
        private void UpdateFlushInfo()
        {
            var flushInfo = FLog.FlushInfo;
            txtFlushInfo.Text = $"每百条日志写入时间: {flushInfo.TotalTime / (flushInfo.LogCount > 0 ? flushInfo.LogCount : 1) * 100:F3} ms";
        }

        /// <summary>
        /// 持续记录磁盘信息日志。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async Task WriteDiskInfoAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var drives = System.IO.DriveInfo.GetDrives();
                    var info = string.Join(", ", drives.Where(d => d.IsReady).Select(d => $"{d.Name} {d.TotalFreeSpace / 1024 / 1024}MB/{d.TotalSize / 1024 / 1024}MB"));
                    var log = new Log(GetSelectedWriteLogLevel(), "磁盘信息：{0}", info).WithCallerInfo();
                    FLog.Add(log);
                    await Task.Delay(5, token);
                }
            }
            catch (Exception ex)
            {
                FLog.Add(new Log(LogLevel.Error, "磁盘信息写入异常：{0}", ex.ToString()).WithCallerInfo());
            }
        }

        /// <summary>
        /// 持续记录除法日志。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async Task WriteDivisionAsync(CancellationToken token)
        {
            try
            {
                var rand = new Random();
                while (!token.IsCancellationRequested)
                {
                    int a = rand.Next(10);
                    int b = rand.Next(1, 10);
                    double result = 1.0 * a / b;
                    var log = new Log(GetSelectedWriteLogLevel(), "除法日志：{0} / {1} = {2:F2}", a, b, result).WithCallerInfo();
                    FLog.Add(log);
                    await Task.Delay(5, token);
                }
            }
            catch (Exception ex)
            {
                FLog.Add(new Log(LogLevel.Error, "除法日志写入异常：{0}", ex.ToString()).WithCallerInfo());
            }
        }

        /// <summary>
        /// 持续记录长文本日志。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async Task WriteLongTextAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var text = new string('长', 100) + DateTime.Now;
                    var log = new Log(GetSelectedWriteLogLevel(), "长文本：{0}", text).WithCallerInfo();
                    FLog.Add(log);
                    await Task.Delay(5, token);
                }
            }
            catch (Exception ex)
            {
                FLog.Add(new Log(LogLevel.Error, "长文本写入异常：{0}", ex.ToString()).WithCallerInfo());
            }
        }

        /// <summary>
        /// 持续记录乘法日志。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async Task WriteMultiplicationAsync(CancellationToken token)
        {
            try
            {
                var rand = new Random();
                while (!token.IsCancellationRequested)
                {
                    int a = rand.Next(10);
                    int b = rand.Next(10);
                    int result = a * b;
                    var log = new Log(GetSelectedWriteLogLevel(), "乘法日志：{0} x {1} = {2}", a, b, result).WithCallerInfo();
                    FLog.Add(log);
                    await Task.Delay(5, token);
                }
            }
            catch (Exception ex)
            {
                FLog.Add(new Log(LogLevel.Error, "乘法日志写入异常：{0}", ex.ToString()).WithCallerInfo());
            }
        }

        /// <summary>
        /// 持续记录网络信息日志。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async Task WriteNetworkInfoAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    string? host = null;
                    string? ip = null;
                    try
                    {
                        host = System.Net.Dns.GetHostName();
                        var ips = System.Net.Dns.GetHostAddresses(host)
                            .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            .Select(a => a.ToString());
                        ip = string.Join("/", ips);
                    }
                    catch { }
                    var log = new Log(GetSelectedWriteLogLevel(), "网络信息：主机 {0}, IP {1}", host ?? "未知", ip ?? "未知").WithCallerInfo();
                    FLog.Add(log);
                    await Task.Delay(5, token);
                }
            }
            catch (Exception ex)
            {
                FLog.Add(new Log(LogLevel.Error, "网络信息写入异常：{0}", ex.ToString()).WithCallerInfo());
            }
        }

        /// <summary>
        /// 持续记录随机字符串日志。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async Task WriteRandomStringAsync(CancellationToken token)
        {
            try
            {
                var rand = new Random();
                while (!token.IsCancellationRequested)
                {
                    var str = Guid.NewGuid().ToString();
                    var log = new Log(GetSelectedWriteLogLevel(), "随机字符串：{0}", str).WithCallerInfo();
                    FLog.Add(log);
                    await Task.Delay(5, token);
                }
            }
            catch (Exception ex)
            {
                FLog.Add(new Log(LogLevel.Error, "随机字符串写入异常：{0}", ex.ToString()).WithCallerInfo());
            }
        }

        /// <summary>
        /// 持续记录短文本日志。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async Task WriteShortTextAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var text = "短文本" + DateTime.Now.Second;
                    var log = new Log(GetSelectedWriteLogLevel(), "短文本：{0}", text).WithCallerInfo();
                    FLog.Add(log);
                    await Task.Delay(5, token);
                }
            }
            catch (Exception ex)
            {
                FLog.Add(new Log(LogLevel.Error, "短文本写入异常：{0}", ex.ToString()).WithCallerInfo());
            }
        }

        /// <summary>
        /// 持续记录系统信息日志。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async Task WriteSystemInfoAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var os = Environment.OSVersion.ToString();
                    var is64 = Environment.Is64BitOperatingSystem ? "64位" : "32位";
                    var machine = Environment.MachineName;
                    var user = Environment.UserName;
                    var processorCount = Environment.ProcessorCount;
                    var dotnet = Environment.Version.ToString();
                    var totalMemory = GC.GetTotalMemory(false) / 1024 / 1024;
                    var log = new Log(GetSelectedWriteLogLevel(), "系统信息 | OS: {0} ({1}), 计算机: {2}, 用户: {3}, CPU核心: {4}, .NET: {5}, 进程内存: {6}MB", os, is64, machine, user, processorCount, dotnet, totalMemory).WithCallerInfo();
                    FLog.Add(log);
                    await Task.Delay(5, token);
                }
            }
            catch (Exception ex)
            {
                FLog.Add(new Log(LogLevel.Error, "系统信息写入异常：{0}", ex.ToString()).WithCallerInfo());
            }
        }

        /// <summary>
        /// 持续记录时间戳日志。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async Task WriteTimestampAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var log = new Log(GetSelectedWriteLogLevel(), "时间戳：{0}", ts).WithCallerInfo();
                    FLog.Add(log);
                    await Task.Delay(5, token);
                }
            }
            catch (Exception ex)
            {
                FLog.Add(new Log(LogLevel.Error, "时间戳写入异常：{0}", ex.ToString()).WithCallerInfo());
            }
        }

        /// <summary>
        /// 持续记录用户信息日志。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async Task WriteUserInfoAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var user = Environment.UserName;
                    var domain = Environment.UserDomainName;
                    var log = new Log(GetSelectedWriteLogLevel(), "用户信息：{0}@{1}", user, domain).WithCallerInfo();
                    FLog.Add(log);
                    await Task.Delay(5, token);
                }
            }
            catch (Exception ex)
            {
                FLog.Add(new Log(LogLevel.Error, "用户信息写入异常：{0}", ex.ToString()).WithCallerInfo());
            }
        }

        /// <summary>
        /// 日志内容TextBlock的Loaded事件处理。
        /// </summary>
        /// <param name="sender">事件源对象。</param>
        /// <param name="e">事件参数。</param>
        private void LogTextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock tb && tb.DataContext is LogViewModel vm)
            {
                tb.Inlines.Clear();
                foreach (var inline in vm.Inlines)
                    tb.Inlines.Add(inline);
            }
        }

        /// <summary>
        /// 初始化 MainWindow 实例。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            // 初始化刷新定时器
            _flushInfoTimer = new DispatcherTimer();
            _flushInfoTimer.Interval = TimeSpan.FromMilliseconds(500);
            _flushInfoTimer.Tick += (s, e) => UpdateFlushInfo();
            _flushInfoTimer.Start();
        }
    }
}