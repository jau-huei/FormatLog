using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Linq.Expressions;

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
        /// 日志双缓冲队列B。与队列A交替作为写入或处理队列，实现高效批量日志处理。
        /// </summary>
        private static ConcurrentQueue<Log> _logQueueB = new();

        /// <summary>
        /// 当前活跃的日志写入队列。Flush 时会原子切换，保证日志写入与批量处理互不阻塞。
        /// </summary>
        private static volatile ConcurrentQueue<Log> _logQueueActive = _logQueueA;

        /// <summary>
        /// 后台日志写入任务。
        /// </summary>
        private static Task? _workerTask;

        /// <summary>
        /// 批量插入实体集合到数据库，并自动提交事务。
        /// 该方法会根据实体类型自动生成批量插入 SQL 语句（多行 VALUES），
        /// 适用于高效写入 Format、Argument、CallerInfo、Log 等实现了 <see cref="ISqlInsertable"/> 的实体。
        /// </summary>
        /// <typeparam name="T">实体类型，必须实现 <see cref="ISqlInsertable"/>。</typeparam>
        /// <param name="dbSet">目标数据库表的 DbSet。</param>
        /// <param name="entities">待插入的实体集合。</param>
        private static async Task AddRangeAndCommitAsync<T>(DbSet<T> dbSet, List<T> entities) where T : class, ISqlInsertable
        {
            if (entities == null || entities.Count == 0) return;

            var context = dbSet.GetService<ICurrentDbContext>().Context;
            var conn = context.Database.GetDbConnection();
            await conn.OpenAsync();

            var sb = new System.Text.StringBuilder();
            sb.Append(entities.First().GetInsertSql());
            var valuesSql = entities.AsParallel().Select(e => e.ToValueSql()).ToList();
            sb.Append(string.Join(",", valuesSql));

            using var tran = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tran;
            cmd.CommandText = sb.ToString();
            await cmd.ExecuteNonQueryAsync();
            tran.Commit();
        }

        /// <summary>
        /// 立即将日志池中剩余的日志批量异步写入指定日期的数据库文件，
        /// 并确保相关的格式、参数、调用者信息等副表数据完整持久化。
        /// 如写入过程中发生异常，将异常信息和未写入日志持久化到日志目录以便排查。
        /// </summary>
        /// <param name="date">目标数据库日期（用于选择写入的数据库文件）。</param>
        /// <param name="token">取消操作的令牌。</param>
        private static async Task FlushAsync(DateTime date, CancellationToken token = default)
        {
            // 交换队列
            var processingQueue = Interlocked.Exchange(ref _logQueueActive, _logQueueActive == _logQueueA ? _logQueueB : _logQueueA);

            var logs = new List<Log>();
            try
            {
                var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
                while (processingQueue.TryDequeue(out var log))
                    logs.Add(log);

                logs = logs.OrderBy(l => l.CreatedAt).ToList();

                if (logs.Count > 0)
                {
                    // 建立数据库
                    using var db = new LogDbContext(date.Year, date.Month, date.Day);
                    await db.Database.EnsureCreatedAsync(token);

                    // 准备副表
                    var prepareStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var formats = logs.Select(l => l.Format!).Where(f => f != null).Distinct().ToList();
                    formats = await GetOrCreateEntitiesAsync(
                        db, formats,
                        d => d.Formats,
                        f => (x => x.FormatString == f.FormatString)
                    );
                    var formatDic = formats.ToDictionary(f => f, f => f);

                    var args = logs.SelectMany(l => l.GetNotNullArguments()).Distinct().ToList();
                    args = await GetOrCreateEntitiesAsync(
                        db, args,
                        d => d.Arguments,
                        a => (x => x.Value == a.Value)
                    );
                    var argsDic = args.ToDictionary(a => a, a => a);

                    var callers = logs.Select(l => l.CallerInfo!).Where(c => c != null).Distinct().ToList();
                    callers = await GetOrCreateEntitiesAsync(
                        db, callers,
                        d => d.CallerInfos,
                        c => (x => x.MemberName == c.MemberName && x.SourceFilePath == c.SourceFilePath && x.SourceLineNumber == c.SourceLineNumber)
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

                    var writeStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    await AddRangeAndCommitAsync(
                        db.Set<Log>(),
                        logs
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
                await HandleFlushExceptionAsync(date, logs, ex);
            }
        }

        /// <summary>
        /// 通用辅助方法：根据唯一性表达式批量查询或创建实体，确保数据库中存在所有指定实体（如日志格式、参数、调用者信息等）。
        /// 对每个实体，先根据唯一性表达式在数据库查找，若不存在则插入，最终返回所有已存在或新建的实体（含主键）。
        /// 适用于副表去重、批量持久化等场景，避免重复数据。
        /// </summary>
        /// <typeparam name="T">实体类型（如 Format、Argument、CallerInfo 等）</typeparam>
        /// <param name="db">日志数据库上下文</param>
        /// <param name="entities">待确保存在的实体列表</param>
        /// <param name="dbSetSelector">用于获取目标 DbSet 的选择器</param>
        /// <param name="uniqueExpressionFactory">唯一性表达式工厂，根据实体生成用于数据库查找的表达式</param>
        /// <returns>数据库中已存在或新建的实体列表（含主键）</returns>
        private static async Task<List<T>> GetOrCreateEntitiesAsync<T>(LogDbContext db, List<T> entities, Func<LogDbContext, DbSet<T>> dbSetSelector, Func<T, Expression<Func<T, bool>>> uniqueExpressionFactory) where T : class, ISqlInsertable
        {
            if (entities.Count == 0)
                return new List<T>();

            var dbSet = dbSetSelector(db);

            // 1. 批量插入（已存在的会被忽略）
            await AddRangeAndCommitAsync(dbSet, entities);

            // 2. 查询所有实体（带主键）
            var result = new List<T>();
            foreach (var entity in entities)
            {
                var expr = uniqueExpressionFactory(entity);
                var found = await dbSet.FirstOrDefaultAsync(expr);
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

        /// <summary>
        /// 进程退出时自动触发日志池批量写入操作。
        /// 当程序关闭时，调用 <see cref="FlushAsync"/> 方法，将当前日志队列中的所有日志异步写入当天的数据库文件，
        /// 并持久化相关副表数据（格式、参数、调用者信息等），提升日志数据的可靠性和完整性。
        /// 注意：由于进程退出时异步操作可能未完全执行完毕，建议如需确保日志完整写入，可在退出前主动调用同步等待的 Flush 方法。
        /// </summary>
        private static void OnProcessExit(object? sender, EventArgs e)
        {
            _ = FlushAsync(DateTime.Today);
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
                Directory.CreateDirectory(LogDbContext.DbDirectory);

                _cts = new CancellationTokenSource();
                _workerTask = Task.Run(() => WorkerLoop(_cts.Token));
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                _initialized = true;
            }
        }

        /// <summary>
        /// 根据游标分页参数异步获取日志，支持双向分页（上一页/下一页）和升序/降序排序。
        /// 根据 OrderType 决定排序方向，根据 NextCursorId/PrevCursorId 决定游标过滤。
        /// - NextCursorId 有值时，表示下一页，按当前排序方向游标过滤。
        /// - PrevCursorId 有值时，表示上一页，反向排序并取 pageSize 条，最后反转结果。
        /// - 只允许 NextCursorId 或 PrevCursorId 有一个有值。
        /// - 支持多条件筛选（级别、时间、格式、调用者、参数等）。
        /// 返回 KeysetPage<Log>，包含当前页数据及上一页/下一页游标。
        /// </summary>
        /// <param name="queryModel">查询参数。</param>
        /// <returns>游标分页日志结果。</returns>
        public static async Task<KeysetPage<Log>> KeysetPaginationAsync(this QueryModel queryModel)
        {
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
                .AsNoTracking()
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
                var formatIds = await db.Formats.AsNoTracking().Where(f => f.FormatString.Contains(queryModel.FormatString)).Select(f => f.Id).ToListAsync();
                logs = logs.Where(l => formatIds.Contains(l.FormatId));
            }

            if (!string.IsNullOrWhiteSpace(queryModel.CallerInfo))
            {
                var callerIds = await db.CallerInfos.AsNoTracking().Where(c => c.MemberName!.Contains(queryModel.CallerInfo) || c.SourceFilePath!.Contains(queryModel.CallerInfo)).Select(f => f.Id).ToListAsync();
                logs = logs.Where(l => l.CallerInfoId.HasValue && callerIds.Contains(l.CallerInfoId.Value));
            }

            if (!string.IsNullOrWhiteSpace(queryModel.Argument))
            {
                var argIds = await db.Arguments.AsNoTracking().Where(arg => arg.Value!.Contains(queryModel.Argument)).Select(arg => arg.Id).ToListAsync();
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

            int pageSize = queryModel.PageSize > 0 ? queryModel.PageSize : 20;
            List<Log> items;
            bool isAscending = queryModel.OrderType == OrderType.OrderByIdAscending;

            // 双向分页逻辑
            if (queryModel.PrevCursorId.HasValue)
            {
                // 上一页，反向排序，游标过滤
                if (isAscending)
                {
                    logs = logs.Where(l => l.Id <= queryModel.PrevCursorId.Value).OrderByDescending(l => l.Id);
                }
                else
                {
                    logs = logs.Where(l => l.Id >= queryModel.PrevCursorId.Value).OrderBy(l => l.Id);
                }
                items = await logs.Take(pageSize).ToListAsync();
                items.Reverse(); // 反转为正常显示顺序
            }
            else
            {
                // 下一页或首页
                if (queryModel.NextCursorId.HasValue)
                {
                    if (isAscending)
                        logs = logs.Where(l => l.Id >= queryModel.NextCursorId.Value);
                    else
                        logs = logs.Where(l => l.Id <= queryModel.NextCursorId.Value);
                }
                logs = isAscending ? logs.OrderBy(l => l.Id) : logs.OrderByDescending(l => l.Id);
                items = await logs.Take(pageSize).ToListAsync();
            }

            long? nextCursorId = null;
            long? prevCursorId = null;
            if (items.Count > 0)
            {
                prevCursorId = items.First().Id;
                nextCursorId = items.Last().Id;
            }

            return new KeysetPage<Log>
            {
                Items = items,
                PreCursorId = prevCursorId,
                NextCursorId = nextCursorId,
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