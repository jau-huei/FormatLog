using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Linq.Expressions;
using System.Diagnostics;

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
        /// 用于取消后台日志写入任务的令牌源。
        /// </summary>
        private static CancellationTokenSource? _cts;

        /// <summary>
        /// 标记是否已初始化后台日志写入。
        /// </summary>
        private static bool _initialized = false;

        /// <summary>
        /// 日志双缓冲队列A。用于收集待写入数据库的日志。
        /// </summary>
        private static ConcurrentQueue<Log> _logQueueA = new();

        /// <summary>
        /// 当前活跃的日志写入队列。Flush 时会原子切换，保证日志写入与批量处理互不阻塞。
        /// </summary>
        private static volatile ConcurrentQueue<Log> _logQueueActive = _logQueueA;

        /// <summary>
        /// 日志双缓冲队列B。与队列A交替作为写入或处理队列，实现高效批量日志处理。
        /// </summary>
        private static ConcurrentQueue<Log> _logQueueB = new();

        /// <summary>
        /// 后台日志写入任务。
        /// </summary>
        private static Task? _workerTask;

        /// <summary>
        /// 批量插入日志区间统计数据，若区间已存在则自动累加 LogCount。
        /// </summary>
        /// <param name="db">数据库上下文。</param>
        /// <param name="stats">待插入的区间统计列表。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private static async Task AddOrUpdateLogIntervalStatsAsync(LogDbContext db, List<LogIntervalStat> stats, CancellationToken token = default)
        {
            if (stats == null || stats.Count == 0) return;

            var sqlList = new List<string>();
            foreach (var stat in stats)
            {
                var interval = stat.IntervalStart.ToString("yyyy-MM-dd HH:mm:ss");
                var count = stat.LogCount;
                sqlList.Add(
                    $"INSERT INTO LogIntervalStats (IntervalStart, LogCount) VALUES ('{interval}', {count}) " +
                    "ON CONFLICT(IntervalStart) DO UPDATE SET LogCount = LogIntervalStats.LogCount + excluded.LogCount;"
                );
            }

            var sql = string.Join("\n", sqlList);
            await db.Database.ExecuteSqlRawAsync(sql, token);
        }

        /// <summary>
        /// 批量插入实体集合到数据库，并自动提交事务。
        /// 该方法会根据实体类型自动生成批量插入 SQL 语句（多行 VALUES），
        /// 适用于高效写入 Format、Argument、CallerInfo、Log 等实现了 <see cref="ISqlInsertable"/> 的实体。
        /// </summary>
        /// <typeparam name="T">实体类型，必须实现 <see cref="ISqlInsertable"/>。</typeparam>
        /// <param name="db">日志数据库上下文</param>
        /// <param name="dbSet">实体集合</param>
        /// <param name="entities">待插入的实体集合。</param>
        /// <param name="token">取消操作的令牌。</param>
        private static async Task AddRangeAndCommitAsync<T>(LogDbContext db, DbSet<T> dbSet, List<T> entities, CancellationToken token) where T : class, ISqlInsertable
        {
            if (entities == null || entities.Count == 0) return;

            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync(token);

            var sb = new System.Text.StringBuilder();
            sb.Append(entities.First().GetInsertSql());
            var valuesSql = entities.AsParallel().Select(e => e.ToValueSql()).ToList();
            sb.Append(string.Join(",", valuesSql));

            using var tran = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tran;
            cmd.CommandText = sb.ToString();
            await cmd.ExecuteNonQueryAsync(token);
            tran.Commit();
        }

        /// <summary>
        /// 动态等待，最大等待 maxWait 毫秒，期间每 interval 毫秒检查队列长度，
        /// 队列为空时只要 token 未取消就持续等待；
        /// 队列内容大于 2000 时立即跳出；
        /// 队列内容为 1 且等待超过 5 秒时立即跳出；
        /// </summary>
        /// <param name="token">取消令牌。</param>
        private static async Task DynamicDelayAsync(CancellationToken token)
        {
            const int maxWaitMs = 5000;                 // 最大等待 5 秒
            const int checkIntervalMs = 100;            // 每 100ms 检查一次
            const int queueImmediateThreshold = 2000;   // 立即写入阈值

            var start = DateTime.Now;
            while (!token.IsCancellationRequested)
            {
                int waited = (int)(DateTime.Now - start).TotalMilliseconds;
                int count = _logQueueActive.Count;

                // 队列内容为 1 且等待超过 5 秒时立即跳出
                if (count >= 1 && waited >= maxWaitMs)
                    break;

                // 队列内容为 1000 且等待超过 2.5 秒时立即跳出
                if (count >= queueImmediateThreshold / 2 && waited >= maxWaitMs / 2)
                    break;

                // 队列内容大于 2000 时立即跳出
                if (count > queueImmediateThreshold)
                    break;

                await Task.Delay(checkIntervalMs, token);
            }
        }

        /// <summary>
        /// 立即将日志池中剩余的日志批量异步写入指定日期的数据库文件，
        /// 并确保相关的格式、参数、调用者信息等副表数据完整持久化。
        /// 如写入过程中发生异常，将异常信息和未写入日志持久化到日志目录以便排查。
        /// </summary>
        /// <param name="date">目标数据库日期（用于选择写入的数据库文件）。</param>
        /// <param name="token">取消操作的令牌。</param>
        private static async Task FlushAsync(DateTime date, CancellationToken token)
        {
            // 交换队列
            var processingQueue = Interlocked.Exchange(ref _logQueueActive, _logQueueActive == _logQueueA ? _logQueueB : _logQueueA);

            var logs = new List<Log>();
            try
            {
                var totalStopwatch = Stopwatch.StartNew();
                while (processingQueue.TryDequeue(out var log))
                    logs.Add(log);

                logs = logs.OrderBy(l => l.CreatedTick).ToList();

                if (logs.Count > 0)
                {
                    // 建立数据库
                    using var db = new LogDbContext(date.Year, date.Month, date.Day);
                    await db.Database.EnsureCreatedAsync(token);

                    // 准备副表
                    var prepareStopwatch = Stopwatch.StartNew();
                    var formats = logs.Select(l => l.Format!).Where(f => f != null).Distinct().ToList();
                    formats = await GetOrCreateEntitiesAsync(
                        db, db.Formats, formats,
                        f => (x => x.FormatString == f.FormatString),
                        token
                    );
                    var formatDic = formats.ToDictionary(f => f, f => f);

                    var args = logs.SelectMany(l => l.GetNotNullArguments()).Distinct().ToList();
                    args = await GetOrCreateEntitiesAsync(
                        db, db.Arguments, args,
                        a => (x => x.Value == a.Value),
                        token
                    );
                    var argsDic = args.ToDictionary(a => a, a => a);

                    var callers = logs.Select(l => l.CallerInfo!).Where(c => c != null).Distinct().ToList();
                    callers = await GetOrCreateEntitiesAsync(
                        db, db.CallerInfos, callers,
                        c => (x => x.MemberName == c.MemberName && x.SourceFilePath == c.SourceFilePath && x.SourceLineNumber == c.SourceLineNumber),
                        token
                    );
                    var callerDic = callers.ToDictionary(c => c, c => c);
                    prepareStopwatch.Stop();

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

                    var stats = GetLogIntervalStats(logs);

                    var writeStopwatch = Stopwatch.StartNew();

                    // 添加日志
                    await AddRangeAndCommitAsync(
                        db,
                        db.Set<Log>(),
                        logs,
                        token
                    );

                    // 添加日志区间统计
                    await AddOrUpdateLogIntervalStatsAsync(
                        db,
                        stats,
                        token
                    );

                    writeStopwatch.Stop();

                    totalStopwatch.Stop();

                    FlushInfo = new FlushInfo
                    {
                        Date = DateTime.Now,
                        DataPreparationTime = prepareStopwatch.Elapsed.TotalMilliseconds,
                        DataWriteTime = writeStopwatch.Elapsed.TotalMilliseconds,
                        LogCount = logs.Count,
                        TotalTime = totalStopwatch.Elapsed.TotalMilliseconds
                    };
                }
            }
            catch (Exception ex)
            {
                await HandleFlushExceptionAsync(date, logs, ex, token);
            }
        }

        /// <summary>
        /// 按 10 分钟为单位统计日志数量。
        /// 将日志列表按创建时间向下取整到最近的 10 分钟区间分组，
        /// 返回每个区间的起始时间及对应日志数量统计结果。
        /// </summary>
        /// <param name="logs">待统计的日志列表。</param>
        /// <returns>每 10 分钟区间的日志数量统计列表。</returns>
        private static List<LogIntervalStat> GetLogIntervalStats(List<Log> logs)
        {
            // 按 10 分钟区间分组统计
            var result = logs
                .GroupBy(log =>
                {
                    var dt = log.CreatedAt;
                    // 向下取整到最近的 10 分钟
                    var rounded = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute / 10 * 10, 0);
                    return rounded;
                })
                .Select(g => new LogIntervalStat
                {
                    IntervalStart = g.Key,
                    LogCount = g.Count()
                })
                .OrderBy(stat => stat.IntervalStart)
                .ToList();

            return result;
        }

        /// <summary>
        /// 通用辅助方法：根据唯一性表达式批量查询或创建实体，确保数据库中存在所有指定实体（如日志格式、参数、调用者信息等）。
        /// 对每个实体，先根据唯一性表达式在数据库查找，若不存在则插入，最终返回所有已存在或新建的实体（含主键）。
        /// 适用于副表去重、批量持久化等场景，避免重复数据。
        /// </summary>
        /// <typeparam name="T">实体类型（如 Format、Argument、CallerInfo 等）</typeparam>
        /// <param name="db">日志数据库上下文</param>
        /// <param name="dbSet">实体集合</param>
        /// <param name="entities">待确保存在的实体列表</param>
        /// <param name="uniqueExpressionFactory">唯一性表达式工厂，根据实体生成用于数据库查找的表达式</param>
        /// <param name="token">取消操作的令牌。</param>
        /// <returns>数据库中已存在或新建的实体列表（含主键）</returns>
        private static async Task<List<T>> GetOrCreateEntitiesAsync<T>(LogDbContext db, DbSet<T> dbSet, List<T> entities, Func<T, Expression<Func<T, bool>>> uniqueExpressionFactory, CancellationToken token) where T : class, ISqlInsertable
        {
            if (entities.Count == 0)
                return new List<T>();

            // 1. 批量插入（已存在的会被忽略）
            await AddRangeAndCommitAsync(db, dbSet, entities, token);

            // 2. 查询所有实体（带主键）
            var result = new List<T>();
            foreach (var entity in entities)
            {
                var expr = uniqueExpressionFactory(entity);
                var found = await dbSet.FirstOrDefaultAsync(expr, token);
                if (found != null)
                    result.Add(found);
            }
            return result;
        }

        /// <summary>
        /// 当日志批量写入数据库（Flush）发生异常时调用，
        /// 将异常信息和相关日志内容以 JSON 和 TXT 两种格式持久化到日志目录，
        /// 以便后续排查和分析。
        /// </summary>
        /// <param name="date">发生异常的日期。</param>
        /// <param name="logs">本次尝试写入但未成功的日志列表。</param>
        /// <param name="ex">捕获到的异常对象。</param>
        /// <param name="token">取消操作的令牌。</param>
        private static async Task HandleFlushExceptionAsync(DateTime date, List<Log> logs, Exception ex, CancellationToken token)
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

                await File.AppendAllLinesAsync(txtFilePath, contents, token);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 进程退出时自动触发日志池批量写入操作。
        /// 当程序关闭时，调用 <see cref="FlushAsync"/> 方法，将当前日志队列中的所有日志异步写入当天的数据库文件，
        /// 并持久化相关副表数据（格式、参数、调用者信息等），提升日志数据的可靠性和完整性。
        /// 注意：由于进程退出时异步操作可能未完全执行完毕，建议如需确保日志完整写入，可在退出前主动调用同步等待的 Flush 方法。
        /// </summary>
        private static void OnProcessExit(object? sender, EventArgs e)
        {
            _ = FlushAsync(DateTime.Today, new CancellationToken());
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
                await DynamicDelayAsync(token);
            }
        }

        /// <summary>
        /// 当前日志批量写入（Flush）操作的统计信息，包括日期、数据准备耗时、写入耗时和日志数量。
        /// </summary>
        public static FlushInfo FlushInfo { get; private set; } = new FlushInfo();

        /// <summary>
        /// 将日志添加到当前活跃的日志队列（双缓冲），确保高并发下写入与批量处理分离。
        /// </summary>
        public static void Add(Log log)
        {
            if (!_initialized)
                InitLogBackgroundWorker();

            _logQueueActive.Enqueue(log);
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

                _cts = new CancellationTokenSource();
                _workerTask = Task.Run(() => WorkerLoop(_cts.Token));
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                _initialized = true;
            }
        }

        /// <summary>
        /// 获取日志数据库目录下所有有效的日志文件日期列表（文件名格式为 yyyy_MM_dd.sqlite）。
        /// </summary>
        /// <returns>所有存在的日志文件对应的日期集合。</returns>
        public static List<DateOnly> GetLogFiles()
        {
            var dbs = Directory.GetFiles(LogDbContext.DbDirectory, "*.sqlite").ToList();
            if (dbs.Count == 0) return new List<DateOnly>();

            return dbs.Select(db =>
            {
                var fileName = Path.GetFileNameWithoutExtension(db);
                if (DateOnly.TryParseExact(fileName, "yyyy_MM_dd", out var date))
                    return date;
                return DateOnly.MinValue; // 无效日期
            }).Where(d => d != DateOnly.MinValue).ToList();
        }

        /// <summary>
        /// 判断日志数据库目录下是否存在任何日志文件（即是否有可用日志日期）。
        /// </summary>
        /// <returns>如存在日志文件则返回 true，否则返回 false。</returns>
        public static bool HasLogFile()
        {
            return GetLogFiles().Count > 0;
        }

        /// <summary>
        /// 判断指定日期的日志文件（yyyy_MM_dd.sqlite）是否存在于日志数据库目录中。
        /// </summary>
        /// <param name="date">要检查的日志日期。</param>
        /// <returns>如该日期日志文件存在则返回 true，否则返回 false。</returns>
        public static bool HasLogFile(DateOnly date)
        {
            var dbName = $"{date:yyyy_MM_dd}.sqlite";
            return File.Exists(Path.Combine(LogDbContext.DbDirectory, dbName));
        }

        /// <summary>
        /// 判断指定日期的日志文件（yyyy_MM_dd.sqlite）是否存在于日志数据库目录中。
        /// 支持 DateTime 类型日期参数，会自动转换为 DateOnly 并调用对应方法。
        /// </summary>
        /// <param name="date">要检查的日志日期（DateTime 类型）。</param>
        /// <returns>如该日期日志文件存在则返回 true，否则返回 false。</returns>
        public static bool HasLogFile(DateTime date)
        {
            return HasLogFile(DateOnly.FromDateTime(date));
        }

        /// <summary>
        /// 根据游标分页参数异步获取日志，支持双向分页（上一页/下一页）和升序/降序排序。
        /// 根据 OrderType 决定排序方向，根据 NextCursorId/PrevCursorId 决定游标过滤。
        /// - NextCursorId 有值时，表示下一页，按当前排序方向游标过滤。
        /// - PrevCursorId 有值时，表示上一页，反向排序并取 pageSize 条，最后反转结果。
        /// - 只允许 NextCursorId 或 PrevCursorId 有一个有值。
        /// - 支持多条件筛选（级别、时间、格式、调用者、参数等）。
        /// 返回 KeysetPage&lt;Log&gt;，包含当前页数据及上一页/下一页游标。
        /// </summary>
        /// <param name="queryModel">查询参数。</param>
        /// <param name="token">取消操作的令牌。</param>
        /// <returns>游标分页日志结果。</returns>
        public static async Task<KeysetPage<Log>> KeysetPaginationAsync(this QueryModel queryModel, CancellationToken token = default)
        {
            var date = queryModel.StartTime?.Date ?? queryModel.EndTime?.Date ?? DateTime.Today;
            if (!HasLogFile(date)) return new KeysetPage<Log>
            {
                Items = new List<Log>(),
                PreCursorTick = null,
                NextCursorTick = null
            };

            using var db = new LogDbContext(date.Year, date.Month, date.Day);
            await db.Database.EnsureCreatedAsync(token);

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
                .AsNoTracking()
                .AsQueryable();

            // 条件过滤
            if (queryModel.Level.HasValue)
                logs = logs.Where(l => l.Level == queryModel.Level.Value);
            if (queryModel.StartTime.HasValue)
                logs = logs.Where(l => l.CreatedTick >= queryModel.StartTime.Value.Ticks);
            if (queryModel.EndTime.HasValue)
                logs = logs.Where(l => l.CreatedTick <= queryModel.EndTime.Value.Ticks);

            if (!string.IsNullOrWhiteSpace(queryModel.FormatString))
            {
                logs = logs.Where(l =>
                    (l.Format!.FormatString!.Contains(queryModel.FormatString))
                );
            }

            if (!string.IsNullOrWhiteSpace(queryModel.CallerInfo))
            {
                logs = logs.Where(l =>
                    (l.CallerInfo!.SourceFilePath!.Contains(queryModel.CallerInfo)) ||
                    (l.CallerInfo!.MemberName!.Contains(queryModel.CallerInfo)) ||
                    (l.CallerInfo!.SourceLineNumber!.ToString()!.Contains(queryModel.CallerInfo))
                );
            }

            if (!string.IsNullOrWhiteSpace(queryModel.Argument))
            {
                logs = logs.Where(l =>
                    (l.Arg0!.Value!.Contains(queryModel.Argument)) ||
                    (l.Arg1!.Value!.Contains(queryModel.Argument)) ||
                    (l.Arg2!.Value!.Contains(queryModel.Argument)) ||
                    (l.Arg3!.Value!.Contains(queryModel.Argument)) ||
                    (l.Arg4!.Value!.Contains(queryModel.Argument)) ||
                    (l.Arg5!.Value!.Contains(queryModel.Argument)) ||
                    (l.Arg6!.Value!.Contains(queryModel.Argument)) ||
                    (l.Arg7!.Value!.Contains(queryModel.Argument)) ||
                    (l.Arg8!.Value!.Contains(queryModel.Argument)) ||
                    (l.Arg9!.Value!.Contains(queryModel.Argument))
                );
            }

            int pageSize = queryModel.PageSize > 0 ? queryModel.PageSize : 20;
            List<Log> items;
            bool isAscending = queryModel.OrderType == OrderType.OrderByIdAscending;

            // 双向分页逻辑
            if (queryModel.PrevCursorTick.HasValue)
            {
                // 上一页，反向排序，游标过滤
                if (isAscending)
                    logs = logs.Where(l => l.CreatedTick <= queryModel.PrevCursorTick.Value).OrderByDescending(l => l.CreatedTick);
                else
                    logs = logs.Where(l => l.CreatedTick >= queryModel.PrevCursorTick.Value).OrderBy(l => l.CreatedTick);
                items = await logs.Take(pageSize).ToListAsync(token);
                items.Reverse(); // 反转为正常显示顺序
            }
            else
            {
                // 下一页或首页
                if (queryModel.NextCursorTick.HasValue)
                {
                    if (isAscending)
                        logs = logs.Where(l => l.CreatedTick >= queryModel.NextCursorTick.Value);
                    else
                        logs = logs.Where(l => l.CreatedTick <= queryModel.NextCursorTick.Value);
                }
                logs = isAscending ? logs.OrderBy(l => l.CreatedTick) : logs.OrderByDescending(l => l.CreatedTick);
                items = await logs.Take(pageSize).ToListAsync(token);
            }

            var qStr = logs.Take(pageSize).ToQueryString();

            long? nextCursorTick = null;
            long? prevCursorTick = null;
            if (items.Count > 0)
            {
                prevCursorTick = items.First().CreatedTick;
                nextCursorTick = items.Last().CreatedTick;
            }

            return new KeysetPage<Log>
            {
                Items = items,
                PreCursorTick = prevCursorTick,
                NextCursorTick = nextCursorTick,
                TotalRecords = await db.Logs.MaxAsync(log => (long?)log.Id) ?? 0L
            };
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