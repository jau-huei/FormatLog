using FormatLog;
using System.Diagnostics;

namespace Demo
{
    /// <summary>
    /// 演示日志系统功能的主程序类，包括多线程生成乘法与除法日志、定期采集系统信息、以及循环查询和显示日志。
    ///
    /// 主要功能：
    /// 1. 多线程异步生成不同类型的乘法和除法日志，并写入日志系统。
    /// 2. 定期采集操作系统、CPU、内存、磁盘、网络等系统信息并记录为日志。
    /// 3. 持续查询日志并以分页方式刷新显示在控制台，支持自定义格式和参数。
    ///
    /// 适用于演示日志记录、查询、格式化输出等功能的实现方式。
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// 持续生成并写入除法日志。
        /// </summary>
        private static async Task DivisionAsync(string format, int n)
        {
            var rand = new Random();
            while (true)
            {
                int a = rand.Next(n);
                int b = rand.Next(1, n); // 避免除数为0
                double result = 1.0 * a / b;
                var log = new Log(format, a, b, result.ToString("F2")).WithCallerInfo();
                FLog.Add(log);
                await Task.Delay(10);
            }
        }

        /// <summary>
        /// 获取字符串的显示宽度。
        /// </summary>
        /// <param name="s">输入字符串</param>
        /// <returns>显示宽度</returns>
        private static int GetDisplayWidth(string s)
        {
            int width = 0;
            foreach (var c in s)
            {
                // CJK 统一表意符号、全角标点等，宽度为2
                if (c >= 0x4E00 && c <= 0x9FFF || // CJK
                    c >= 0x3400 && c <= 0x4DBF || // CJK 扩展A
                    c >= 0x20000 && c <= 0x2A6DF || // CJK 扩展B
                    c >= 0x2A700 && c <= 0x2B73F || // CJK 扩展C
                    c >= 0x2B740 && c <= 0x2B81F || // CJK 扩展D
                    c >= 0x2B820 && c <= 0x2CEAF || // CJK 扩展E
                    c >= 0xF900 && c <= 0xFAFF || // CJK 兼容
                    c >= 0xFF01 && c <= 0xFF60 || // 全角符号
                    c >= 0xFFE0 && c <= 0xFFE6)   // 全角符号
                    width += 2;
                else
                    width += 1;
            }
            return width;
        }

        /// <summary>
        /// 持续生成并写入乘法日志。
        /// </summary>
        private static async Task MultiplicationAsync(string format, int n)
        {
            var rand = new Random();
            while (true)
            {
                int a = rand.Next(n);
                int b = rand.Next(n);
                int result = a * b;
                var log = new Log(format, a, b, result).WithCallerInfo();
                FLog.Add(log);
                await Task.Delay(10);
            }
        }

        /// <summary>
        /// 查表循环函数，持续查询日志并刷新显示。
        /// </summary>
        private static async Task QueryAndDisplayLogsLoop()
        {
            int lastLineCount = 0;

            var queryModel = new QueryModel()
                .WithFormat("八八乘法")
                .WithArgs("4")
                .WithPageIndex(1)
                .OrderBy(OrderType.OrderByTimeDescending);

            while (true)
            {
                var emptyLine = new string(' ', Console.WindowWidth);

                var stopWatch = Stopwatch.StartNew();
                var pageResult = await queryModel.GetPagedLogsAsync();
                stopWatch.Stop();

                Console.SetCursorPosition(0, 0);
                var lines = new List<string>
                {
                    $"查询时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}",
                    $"查询结果：{pageResult.TotalCount} 条日志，当前页 {pageResult.PageIndex}/{pageResult.TotalPages}，每页 {pageResult.PageSize} 条。耗时 {stopWatch.ElapsedMilliseconds:F3} ms",
                };
                Console.WriteLine(emptyLine);
                lines.AddRange(pageResult.Items.Select(log => log.ToString()));

                foreach (var line in lines)
                {
                    var len = emptyLine.Length - GetDisplayWidth(line);
                    if (len <= 0)
                        Console.WriteLine(line);
                    else
                        Console.WriteLine(line + new string(' ', len));
                }

                // 用空行覆盖旧内容
                for (int i = lines.Count; i < lastLineCount; i++)
                    Console.WriteLine(emptyLine);

                lastLineCount = lines.Count;
                queryModel.WithPageIndex(5 * (pageResult.TotalPages / 10) + 1);

                await Task.Delay(10);
            }
        }

        /// <summary>
        /// 定期采集并记录系统信息（如操作系统、CPU、内存、磁盘、网络等），以日志形式存储。
        /// </summary>
        private static async Task RecoderSystemInfoAsync()
        {
            while (true)
            {
                // 操作系统与环境信息
                var os = Environment.OSVersion.ToString();
                var is64 = Environment.Is64BitOperatingSystem ? "64位" : "32位";
                var machine = Environment.MachineName;
                var user = Environment.UserName;
                var processorCount = Environment.ProcessorCount;
                var dotnet = Environment.Version.ToString();

                // 内存信息
                var totalMemory = GC.GetTotalMemory(false) / 1024 / 1024; // MB

                // 磁盘信息
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady)
                    .Select(d => $"{d.Name} {d.TotalFreeSpace / 1024 / 1024}MB/{d.TotalSize / 1024 / 1024}MB")
                    .ToArray();
                var diskInfo = string.Join(", ", drives);

                // 网络信息
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

                // 组装日志内容
                var log = new Log(
                    "系统信息 | OS: {0} ({1}), 计算机: {2}, 用户: {3}, CPU核心: {4}, .NET: {5}, 进程内存: {6}MB, 磁盘: {7}, 主机: {8}, IP: {9}",
                    os, is64, machine, user, processorCount, dotnet, totalMemory, diskInfo, host ?? "未知", ip ?? "未知"
                ).WithCallerInfo();

                FLog.Add(log);

                await Task.Delay(10000); // 10秒采集一次
            }
        }

        public static void Main(string[] args)
        {
            // 多线程日志写入测试
            // 小内容数据
            _ = Task.Run(() => MultiplicationAsync("三三乘法： {0} x {1} = {2}", 3));
            _ = Task.Run(() => MultiplicationAsync("四四乘法： {0} x {1} = {2}", 4));
            _ = Task.Run(() => MultiplicationAsync("五五乘法： {0} x {1} = {2}", 5));
            _ = Task.Run(() => MultiplicationAsync("六六乘法： {0} x {1} = {2}", 6));
            _ = Task.Run(() => MultiplicationAsync("七七乘法： {0} x {1} = {2}", 7));
            _ = Task.Run(() => MultiplicationAsync("八八乘法： {0} x {1} = {2}", 8));
            _ = Task.Run(() => MultiplicationAsync("九九乘法： {0} x {1} = {2}", 9));

            _ = Task.Run(() => DivisionAsync("三三除法： {0} / {1} = {2}", 3));
            _ = Task.Run(() => DivisionAsync("四四除法： {0} / {1} = {2}", 4));
            _ = Task.Run(() => DivisionAsync("五五除法： {0} / {1} = {2}", 5));
            _ = Task.Run(() => DivisionAsync("六六除法： {0} / {1} = {2}", 6));
            _ = Task.Run(() => DivisionAsync("七七除法： {0} / {1} = {2}", 7));
            _ = Task.Run(() => DivisionAsync("八八除法： {0} / {1} = {2}", 8));
            _ = Task.Run(() => DivisionAsync("九九除法： {0} / {1} = {2}", 9));

            // 大内容数据
            _ = Task.Run(() => RecoderSystemInfoAsync());

            // 用 Task.Run 启动查表任务
            _ = Task.Run(() => QueryAndDisplayLogsLoop());

            Console.ReadKey();
        }
    }
}