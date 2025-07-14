using FormatLog;
using System.Diagnostics;

namespace Demo
{
    /// <summary>
    /// 演示日志系统功能的主程序类，包括多线程生成乘法与除法日志、定期采集系统信息、以及循环查询和显示日志。
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// 持续生成并写入除法日志。
        /// 随机生成两个整数进行除法运算，结果写入日志系统。
        /// </summary>
        /// <param name="format">日志格式字符串。</param>
        /// <param name="n">随机数上限。</param>
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
        /// 支持中英文混排，CJK字符宽度为2，其他为1。
        /// </summary>
        /// <param name="s">输入字符串。</param>
        /// <returns>显示宽度。</returns>
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
        /// 随机生成两个整数进行乘法运算，结果写入日志系统。
        /// </summary>
        /// <param name="format">日志格式字符串。</param>
        /// <param name="n">随机数上限。</param>
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
        /// 获取写入统计字段（flushInfo相关字段）。
        /// 返回最新批次日志写入统计信息的字符串列表。
        /// </summary>
        /// <param name="flushInfo">日志批量写入统计信息。</param>
        /// <returns>统计信息字符串列表。</returns>
        private static List<string> GetFlushInfoLines(FlushInfo flushInfo)
        {
            return new List<string>
            {
                $"最新批次日志写入数量：{flushInfo.LogCount}",
                $"日志准备时间：{flushInfo.DataPreparationTime:F3} ms",
                $"日志写入时间：{flushInfo.DataWriteTime:F3} ms",
                $"总耗时：{flushInfo.TotalTime:F3} ms",
                $"平均每百条日志耗时：{(flushInfo.LogCount > 0 ? 100 * flushInfo.TotalTime / flushInfo.LogCount : 0):F3} ms",
            };
        }

        /// <summary>
        /// 获取查询结果字段（pageResult相关字段）。
        /// 返回当前分页查询结果的统计信息和日志内容。
        /// </summary>
        /// <param name="pageResult">分页查询结果。</param>
        /// <param name="stopWatch">查询耗时计时器。</param>
        /// <returns>查询结果字符串列表。</returns>
        private static List<string> GetQueryResultLines(KeysetPage<Log> pageResult, Stopwatch stopWatch)
        {
            var lines = new List<string>
            {
                $"查询时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}",
                $"查询结果：{pageResult.Items.Count} 条日志，下一页游标Id: {pageResult.NextCursorId}, 下一页游标时间: {pageResult.NextCursorCreatedAt}，耗时 {stopWatch.ElapsedMilliseconds:F3} ms"
            };
            lines.AddRange(pageResult.Items.Select(log => log.ToString()));
            return lines;
        }

        /// <summary>
        /// 展示所有行到控制台。
        /// 按指定宽度和格式输出所有字符串行。
        /// </summary>
        /// <param name="lines">要显示的字符串列表。</param>
        /// <param name="lastLineCount">上一次显示的行数。</param>
        /// <param name="windowWidth">控制台窗口宽度。</param>
        /// <param name="getDisplayWidth">获取字符串显示宽度的委托。</param>
        private static void DisplayLines(List<string> lines, int lastLineCount, int windowWidth, Func<string, int> getDisplayWidth)
        {
            var emptyLine = new string(' ', windowWidth);
            Console.WriteLine(emptyLine);
            foreach (var line in lines)
            {
                var len = emptyLine.Length - getDisplayWidth(line);
                if (len <= 0)
                    Console.WriteLine(line);
                else
                    Console.WriteLine(line + new string(' ', len));
            }
            // 用空行覆盖旧内容
            for (int i = lines.Count; i < lastLineCount; i++)
                Console.WriteLine(emptyLine);
        }

        /// <summary>
        /// 查表循环函数，持续查询日志并刷新显示。
        /// 持续分页查询日志并刷新控制台显示，支持游标分页。
        /// </summary>
        private static async Task QueryAndDisplayLogsLoop()
        {
            int lastLineCount = 0;
            var queryModel = new QueryModel()
                .WithFormat("八八乘法")
                .WithArgs("4")
                .OrderBy(OrderType.OrderByTimeAscending);
            long? lastId = null;
            while (true)
            {
                queryModel.WithLastId(lastId);
                var stopWatch = Stopwatch.StartNew();
                var pageResult = await queryModel.KeysetPaginationAsync();
                stopWatch.Stop();
                var flushInfo = FLog.FlushInfo;
                Console.SetCursorPosition(0, 0);
                var lines = new List<string>();
                lines.AddRange(GetFlushInfoLines(flushInfo));
                lines.Add("");
                lines.AddRange(GetQueryResultLines(pageResult, stopWatch));
                DisplayLines(lines, lastLineCount, Console.WindowWidth, GetDisplayWidth);
                lastLineCount = lines.Count;
                lastId = pageResult.NextCursorId;
                await Task.Delay(10);
            }
        }

        /// <summary>
        /// 定期采集并记录系统信息（如操作系统、CPU、内存、磁盘、网络等），以日志形式存储。
        /// 每隔10秒采集一次系统信息并写入日志，同时支持游标分页查询系统信息日志。
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

        /// <summary>
        /// 程序入口。
        /// 启动多线程日志写入、系统信息采集和日志查询显示任务。
        /// </summary>
        /// <param name="args">命令行参数。</param>
        public static void Main(string[] args)
        {
            // 多线程日志写入测试
            //// 小内容数据
            //_ = Task.Run(() => MultiplicationAsync("三三乘法： {0} x {1} = {2}", 3));
            //_ = Task.Run(() => MultiplicationAsync("四四乘法： {0} x {1} = {2}", 4));
            //_ = Task.Run(() => MultiplicationAsync("五五乘法： {0} x {1} = {2}", 5));
            //_ = Task.Run(() => MultiplicationAsync("六六乘法： {0} x {1} = {2}", 6));
            //_ = Task.Run(() => MultiplicationAsync("七七乘法： {0} x {1} = {2}", 7));
            //_ = Task.Run(() => MultiplicationAsync("八八乘法： {0} x {1} = {2}", 8));
            //_ = Task.Run(() => MultiplicationAsync("九九乘法： {0} x {1} = {2}", 9));

            //_ = Task.Run(() => DivisionAsync("三三除法： {0} / {1} = {2}", 3));
            //_ = Task.Run(() => DivisionAsync("四四除法： {0} / {1} = {2}", 4));
            //_ = Task.Run(() => DivisionAsync("五五除法： {0} / {1} = {2}", 5));
            //_ = Task.Run(() => DivisionAsync("六六除法： {0} / {1} = {2}", 6));
            //_ = Task.Run(() => DivisionAsync("七七除法： {0} / {1} = {2}", 7));
            //_ = Task.Run(() => DivisionAsync("八八除法： {0} / {1} = {2}", 8));
            //_ = Task.Run(() => DivisionAsync("九九除法： {0} / {1} = {2}", 9));

            //// 大内容数据
            //_ = Task.Run(() => RecoderSystemInfoAsync());

            // 用 Task.Run 启动查表任务
            _ = Task.Run(() => QueryAndDisplayLogsLoop());

            Console.ReadKey();
        }
    }
}