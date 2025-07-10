using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.Json;

namespace FormatLog
{
    /// <summary>
    /// 提供日志池管理、后台批量写入数据库和程序关闭时自动 Flush 的辅助方法。
    /// </summary>
    public static class FLog
    {
        /// <summary>
        /// 用于确保初始化线程安全的锁对象。
        /// </summary>
        private static readonly object _initLock = new();

        /// <summary>
        /// 后台写入数据库的时间间隔。
        /// </summary>
        private static readonly TimeSpan _interval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 日志队列，暂存待写入数据库的日志。
        /// </summary>
        private static readonly ConcurrentQueue<Log> _logQueue = new();

        /// <summary>
        /// 用于取消后台日志写入任务的令牌源。
        /// </summary>
        private static CancellationTokenSource? _cts;

        /// <summary>
        /// 标记是否已初始化后台日志写入。
        /// </summary>
        private static bool _initialized = false;

        /// <summary>
        /// 后台日志写入任务。
        /// </summary>
        private static Task? _workerTask;

        /// <summary>
        /// 上次执行缓存清理操作的时间，用于判断缓存清理的时间间隔和日期变化。
        /// </summary>
        private static DateTime lastRemoveExpired = DateTime.Now;

        /// <summary>
        /// 立即将日志池中剩余的日志批量异步写入指定日期的数据库文件，
        /// 并确保相关的格式、参数、调用者信息等副表数据完整持久化。
        /// 如写入过程中发生异常，将异常信息和未写入日志持久化到日志目录以便排查。
        /// </summary>
        /// <param name="date">目标数据库日期（用于选择写入的数据库文件）。</param>
        /// <param name="token">取消操作的令牌。</param>
        private static async Task FlushAsync(DateTime date, CancellationToken token = default)
        {
            var logs = new List<Log>();
            try
            {
                while (_logQueue.TryDequeue(out var log))
                    logs.Add(log);

                logs = logs.OrderByDescending(l => l.CreatedAt).ToList();

                if (logs.Count > 0)
                {
                    // 建立数据库
                    using var db = new LogDbContext(date.Year, date.Month, date.Day);
                    await db.Database.EnsureCreatedAsync(token);

                    // 准备副表
                    var formats = logs.Select(l => l.Format!).Where(f => f != null).Distinct().ToList();
                    formats = await GetOrCreateFormatsAsync(db, formats);
                    var formatDic = formats.ToDictionary(f => f, f => f);

                    var args = logs.SelectMany(l => l.GetNotNullArguments()).Distinct().ToList();
                    args = await GetOrCreateArgsAsync(db, args);
                    var argsDic = args.ToDictionary(a => a, a => a);

                    var callers = logs.Select(l => l.CallerInfo!).Where(c => c != null).Distinct().ToList();
                    callers = await GetOrCreateCallersAsync(db, callers);
                    var callerDic = callers.ToDictionary(c => c, c => c);

                    // 替换对象
                    foreach (var log in logs)
                    {
                        log.Format = formatDic[log.Format];
                        log.FormatId = log.Format.Id;

                        if (log.CallerInfo != null) log.CallerInfo = callerDic[log.CallerInfo];
                        log.CallerInfoId = log.CallerInfo?.Id;

                        if (log.Arg0 != null) log.Arg0 = argsDic[log.Arg0];
                        log.Arg0Id = log.Arg0?.Id;
                        if (log.Arg1 != null) log.Arg1 = argsDic[log.Arg1];
                        log.Arg1Id = log.Arg1?.Id;
                        if (log.Arg2 != null) log.Arg2 = argsDic[log.Arg2];
                        log.Arg2Id = log.Arg2?.Id;
                        if (log.Arg3 != null) log.Arg3 = argsDic[log.Arg3];
                        log.Arg3Id = log.Arg3?.Id;
                        if (log.Arg4 != null) log.Arg4 = argsDic[log.Arg4];
                        log.Arg4Id = log.Arg4?.Id;
                        if (log.Arg5 != null) log.Arg5 = argsDic[log.Arg5];
                        log.Arg5Id = log.Arg5?.Id;
                        if (log.Arg6 != null) log.Arg6 = argsDic[log.Arg6];
                        log.Arg6Id = log.Arg6?.Id;
                        if (log.Arg7 != null) log.Arg7 = argsDic[log.Arg7];
                        log.Arg7Id = log.Arg7?.Id;
                        if (log.Arg8 != null) log.Arg8 = argsDic[log.Arg8];
                        log.Arg8Id = log.Arg8?.Id;
                        if (log.Arg9 != null) log.Arg9 = argsDic[log.Arg9];
                        log.Arg9Id = log.Arg9?.Id;
                    }

                    await db.Set<Log>().AddRangeAsync(logs, token);
                    await db.SaveChangesAsync(token);
                }
            }
            catch (Exception ex)
            {
                await HandleFlushExceptionAsync(date, logs, ex);
            }
        }

        /// <summary>
        /// 获取或创建参数信息（Argument），确保数据库中存在所有参数。
        /// </summary>
        /// <param name="db">日志数据库上下文。</param>
        /// <param name="args">参数信息列表。</param>
        /// <returns>带有数据库主键的参数信息列表。</returns>
        private static async Task<List<Argument>> GetOrCreateArgsAsync(LogDbContext db, List<Argument> args)
        {
            if (args.Count == 0)
                return new List<Argument>();

            // 提取所有唯一键
            var argValues = args.Select(a => a.Value).ToList();
            var existingArgs = await db.Arguments
                .Where(a => argValues.Contains(a.Value))
                .ToListAsync();

            // 找出本地未在数据库中的
            var notFoundArgs = args
                .Where(a => !existingArgs.Any(ea => ea.Value == a.Value))
                .ToList();

            if (notFoundArgs.Count > 0)
            {
                await db.Arguments.AddRangeAsync(notFoundArgs);
                await db.SaveChangesAsync();
            }

            // 再查一遍，确保全部带Id
            var dbArgs = await db.Arguments
                .Where(a => argValues.Contains(a.Value))
                .ToListAsync();

            return dbArgs;
        }

        /// <summary>
        /// 获取或创建调用者信息（CallerInfo），确保数据库中存在所有调用者信息。
        /// </summary>
        /// <param name="db">日志数据库上下文。</param>
        /// <param name="callers">调用者信息列表。</param>
        /// <returns>带有数据库主键的调用者信息列表。</returns>
        private static async Task<List<CallerInfo>> GetOrCreateCallersAsync(LogDbContext db, List<CallerInfo> callers)
        {
            if (callers.Count == 0)
                return new List<CallerInfo>();

            // 提取所有唯一键
            var memberNames = callers.Select(c => c.MemberName).ToList();
            var filePaths = callers.Select(c => c.SourceFilePath).ToList();
            var lineNumbers = callers.Select(c => c.SourceLineNumber).ToList();

            // 批量查找已存在的
            var existingCallers = await db.CallerInfos
                .Where(c =>
                    memberNames.Contains(c.MemberName) &&
                    filePaths.Contains(c.SourceFilePath) &&
                    lineNumbers.Contains(c.SourceLineNumber))
                .ToListAsync();

            // 找出本地未在数据库中的
            var notFoundCallers = callers
                .Where(c => !existingCallers.Any(ec =>
                    ec.MemberName == c.MemberName &&
                    ec.SourceFilePath == c.SourceFilePath &&
                    ec.SourceLineNumber == c.SourceLineNumber))
                .ToList();

            if (notFoundCallers.Count > 0)
            {
                await db.CallerInfos.AddRangeAsync(notFoundCallers);
                await db.SaveChangesAsync();
            }

            // 再查一遍，确保全部带Id
            var dbCallers = await db.CallerInfos
                .Where(c =>
                    memberNames.Contains(c.MemberName) &&
                    filePaths.Contains(c.SourceFilePath) &&
                    lineNumbers.Contains(c.SourceLineNumber))
                .ToListAsync();

            return dbCallers;
        }

        /// <summary>
        /// 获取或创建格式信息（Format），确保数据库中存在所有格式。
        /// </summary>
        /// <param name="db">日志数据库上下文。</param>
        /// <param name="formats">格式信息列表。</param>
        /// <returns>带有数据库主键的格式信息列表。</returns>
        private static async Task<List<Format>> GetOrCreateFormatsAsync(LogDbContext db, List<Format> formats)
        {
            if (formats.Count == 0)
                return new List<Format>();

            // 提取所有唯一键
            var formatStrings = formats.Select(f => f.FormatString).ToList();
            var existingFormats = await db.Formats
                .Where(f => formatStrings.Contains(f.FormatString))
                .ToListAsync();

            // 找出本地未在数据库中的
            var notFoundFormats = formats
                .Where(f => !existingFormats.Any(ef => ef.FormatString == f.FormatString))
                .ToList();

            if (notFoundFormats.Count > 0)
            {
                await db.Formats.AddRangeAsync(notFoundFormats);
                await db.SaveChangesAsync();
            }

            // 再查一遍，确保全部带Id
            var dbFormats = await db.Formats
                .Where(f => formatStrings.Contains(f.FormatString))
                .ToListAsync();

            return dbFormats;
        }

        /// <summary>
        /// 当日志批量写入数据库（Flush）发生异常时调用，
        /// 将异常信息和相关日志内容以 JSON 和 TXT 两种格式持久化到日志目录，
        /// 以便后续排查和分析。
        /// </summary>
        /// <param name="date">发生异常的日期。</param>
        /// <param name="logs">本次尝试写入但未成功的日志列表。</param>
        /// <param name="ex">捕获到的异常对象。</param>
        private static async Task HandleFlushExceptionAsync(DateTime date, List<Log> logs, Exception ex)
        {
            var fileName = $"Error_{date:yyyy_MM_dd}.{Guid.NewGuid().ToString("N")}.json";

            // 序列化并存到日志文件
            try
            {
                var errorInfo = new LogFlushExceptionInfo
                {
                    Date = date,
                    Logs = logs,
                    ExceptionMessage = ex.Message
                };

                var errorFilePath = Path.Combine(LogDbContext.DbDirectory, fileName);
                var contents = JsonSerializer.Serialize(errorInfo);
                await File.WriteAllTextAsync(errorFilePath, contents);
            }
            catch (Exception)
            {
            }

            // 将错误信息保存到 txt
            try
            {
                var txtFileName = $"Error_{date:yyyy_MM_dd}.txt";
                var txtFilePath = Path.Combine(LogDbContext.DbDirectory, txtFileName);
                var contents = new List<string>()
                {
                    $"[{date:yyyy/MM/dd}]",
                    fileName,
                    ex.Message
                };
                if (!string.IsNullOrEmpty(ex.Source)) contents.Add(ex.Source);
                if (!string.IsNullOrEmpty(ex.StackTrace)) contents.Add(ex.StackTrace);

                await File.AppendAllLinesAsync(txtFilePath, contents);
            }
            catch (Exception)
            {
            }
        }

        private static void OnProcessExit(object? sender, EventArgs e)
        {
            _ = FlushAsync(DateTime.Today);
        }

        /// <summary>
        /// 将日志集合中的每个 <see cref="Log"/> 实例转换为仅包含主键信息和基础字段的新日志对象，
        /// 去除所有导航属性和参数对象，便于批量写入数据库或序列化传输。
        /// </summary>
        /// <param name="logs">要转换的日志集合。</param>
        /// <returns>仅包含主键信息和基础字段的 <see cref="Log"/> 集合。</returns>
        private static IEnumerable<Log> ToKeyOnly(this IEnumerable<Log> logs)
        {
            foreach (Log log in logs)
            {
                yield return log.ToKeyOnly();
            }
        }

        /// <summary>
        /// 后台线程循环，定时批量写入日志到数据库。
        /// </summary>
        /// <param name="token">取消操作的令牌。</param>
        /// <returns>异步任务。</returns>
        private static async Task WorkerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var date = DateTime.Today;
                await FlushAsync(date, token);
                await Task.Delay(_interval, token);
            }
        }

        /// <summary>
        /// 将日志添加到日志池队列。
        /// </summary>
        /// <param name="log">要添加的日志对象。</param>
        public static void Add(Log log)
        {
            if (!_initialized)
                InitLogBackgroundWorker();

            _logQueue.Enqueue(log);
        }

        /// <summary>
        /// 根据查询参数异步分页获取日志。
        /// </summary>
        /// <param name="queryModel">查询参数。</param>
        /// <returns>分页日志结果。</returns>
        public static async Task<PagedResult<Log>> GetPagedLogsAsync(this QueryModel queryModel)
        {
            // 选择数据库日期（优先StartTime，否则EndTime，否则今天）
            var date = queryModel.StartTime?.Date ?? queryModel.EndTime?.Date ?? DateTime.Today;
            using var db = new LogDbContext(date.Year, date.Month, date.Day);

            var logs = db.Logs
                .Include(l => l.Format)
                .Include(l => l.CallerInfo)
                .Include(l => l.Arg0)
                .Include(l => l.Arg1)
                .Include(l => l.Arg2)
                .Include(l => l.Arg3)
                .Include(l => l.Arg4)
                .Include(l => l.Arg5)
                .Include(l => l.Arg6)
                .Include(l => l.Arg7)
                .Include(l => l.Arg8)
                .Include(l => l.Arg9)
                .AsQueryable();

            // 条件过滤
            if (queryModel.Level.HasValue)
                logs = logs.Where(l => l.Level == queryModel.Level.Value);
            if (queryModel.StartTime.HasValue)
                logs = logs.Where(l => l.CreatedAt >= queryModel.StartTime.Value);
            if (queryModel.EndTime.HasValue)
                logs = logs.Where(l => l.CreatedAt <= queryModel.EndTime.Value);

            if (!string.IsNullOrWhiteSpace(queryModel.FormatString))
            {
                var formatIds = await db.Formats.Where(f => f.FormatString.Contains(queryModel.FormatString)).Select(f => f.Id).ToListAsync();
                logs = logs.Where(l => formatIds.Contains(l.FormatId));
            }

            if (!string.IsNullOrWhiteSpace(queryModel.CallerInfo))
            {
                var callerIds = await db.CallerInfos.Where(c => c.MemberName!.Contains(queryModel.CallerInfo) || c.SourceFilePath!.Contains(queryModel.CallerInfo)).Select(f => f.Id).ToListAsync();
                logs = logs.Where(l => l.CallerInfoId.HasValue && callerIds.Contains(l.CallerInfoId.Value));
            }

            if (!string.IsNullOrWhiteSpace(queryModel.Argument))
            {
                var argIds = await db.Arguments.Where(arg => arg.Value!.Contains(queryModel.Argument)).Select(arg => arg.Id).ToListAsync();
                logs = logs.Where(l =>
                    (l.Arg0Id.HasValue && argIds.Contains(l.Arg0Id.Value)) ||
                    (l.Arg1Id.HasValue && argIds.Contains(l.Arg1Id.Value)) ||
                    (l.Arg2Id.HasValue && argIds.Contains(l.Arg2Id.Value)) ||
                    (l.Arg3Id.HasValue && argIds.Contains(l.Arg3Id.Value)) ||
                    (l.Arg4Id.HasValue && argIds.Contains(l.Arg4Id.Value)) ||
                    (l.Arg5Id.HasValue && argIds.Contains(l.Arg5Id.Value)) ||
                    (l.Arg6Id.HasValue && argIds.Contains(l.Arg6Id.Value)) ||
                    (l.Arg7Id.HasValue && argIds.Contains(l.Arg7Id.Value)) ||
                    (l.Arg8Id.HasValue && argIds.Contains(l.Arg8Id.Value)) ||
                    (l.Arg9Id.HasValue && argIds.Contains(l.Arg9Id.Value))
                );
            }

            // 排序
            logs = queryModel.OrderType switch
            {
                OrderType.OrderByTimeAscending => logs.OrderBy(l => l.CreatedAt),
                OrderType.OrderByTimeDescending => logs.OrderByDescending(l => l.CreatedAt),
                OrderType.OrderByLevelAscending => logs.OrderBy(l => l.Level),
                OrderType.OrderByLevelDescending => logs.OrderByDescending(l => l.Level),
                _ => logs.OrderByDescending(l => l.CreatedAt)
            };

            // 分页
            int pageIndex = queryModel.PageIndex > 0 ? queryModel.PageIndex : 1;
            int pageSize = queryModel.PageSize > 0 ? queryModel.PageSize : 20;
            int totalCount = await logs.CountAsync();
            var items = await logs.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();

            return new PagedResult<Log>
            {
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize,
                Items = items
            };
        }

        /// <summary>
        /// 初始化日志后台写入线程（线程安全，避免重复初始化）。
        /// </summary>
        public static void InitLogBackgroundWorker()
        {
            if (_initialized) return;

            lock (_initLock)
            {
                if (_initialized) return;
                Directory.CreateDirectory(LogDbContext.DbDirectory);

                _cts = new CancellationTokenSource();
                _workerTask = Task.Run(() => WorkerLoop(_cts.Token));
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                _initialized = true;
            }
        }

        /// <summary>
        /// 停止后台日志写入线程并释放相关资源。
        /// </summary>
        public static void StopLogBackgroundWorker()
        {
            if (!_initialized) return;

            lock (_initLock)
            {
                if (!_initialized) return;
                _cts?.Cancel();
                _workerTask?.Wait();
                _cts?.Dispose();
                _cts = null;
                _workerTask = null;
                AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                _initialized = false;
            }
        }
    }
}