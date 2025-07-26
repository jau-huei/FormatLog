namespace FormatLog
{
    /// <summary>
    /// 日志查询参数模型，支持基于游标的高效分页检索。
    /// 可用于向前/向后分页查询日志，包含排序、筛选、分页游标等参数。
    /// 未来可扩展为双向分页（上一页/下一页）。
    /// </summary>
    public class QueryModel
    {
        /// <summary>
        /// 获取或设置日志参数的匹配内容。
        /// </summary>
        public string? Argument { get; set; }

        /// <summary>
        /// 获取或设置调用者信息的匹配内容（如成员名、文件路径），用于日志查询时筛选调用上下文。
        /// </summary>
        public string? CallerInfo { get; set; }

        /// <summary>
        /// 获取或设置日志创建时间范围的结束时间。
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 获取或设置日志格式字符串的匹配内容。
        /// </summary>
        public string? FormatString { get; set; }

        /// <summary>
        /// 获取或设置日志级别。
        /// </summary>
        public LogLevel? Level { get; set; }

        /// <summary>
        /// 获取或设置日志排序方式。
        /// </summary>
        public OrderType OrderType { get; set; }

        /// <summary>
        /// 获取或设置每页显示的日志数量。
        /// </summary>
        public int PageSize { get; set; } = 50;

        /// <summary>
        /// 获取或设置日志创建时间范围的起始时间。
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// 游标分页：下一页的游时间戳（用于查询下一页数据时定位起始位置）。
        /// </summary>
        public long? NextCursorTick { get; set; }

        /// <summary>
        /// 游标分页：上一页的游时间戳（用于支持上一页查询）。
        /// </summary>
        public long? PrevCursorTick { get; set; }

        /// <summary>
        /// 设置日志排序方式。
        /// </summary>
        /// <param name="orderType">排序方式。</param>
        /// <returns>返回当前查询模型实例。</returns>
        public QueryModel OrderBy(OrderType orderType)
        {
            OrderType = orderType;
            return this;
        }

        /// <summary>
        /// 设置日志参数的匹配内容。
        /// </summary>
        /// <param name="arguments">参数内容。</param>
        /// <returns>返回当前查询模型实例。</returns>
        public QueryModel WithArgs(string? arguments)
        {
            Argument = arguments;
            return this;
        }

        /// <summary>
        /// 设置调用者信息的匹配内容，用于日志查询时筛选调用上下文。
        /// </summary>
        /// <param name="callerInfo">调用者信息（如成员名、文件路径等）。</param>
        /// <returns>返回当前查询模型实例。</returns>
        public QueryModel WithCaller(string? callerInfo)
        {
            CallerInfo = callerInfo;
            return this;
        }

        /// <summary>
        /// 设置日志格式字符串的匹配内容。
        /// </summary>
        /// <param name="formatString">格式字符串。</param>
        /// <returns>返回当前查询模型实例。</returns>
        public QueryModel WithFormat(string? formatString)
        {
            FormatString = formatString;
            return this;
        }

        /// <summary>
        /// 设置日志级别。
        /// </summary>
        /// <param name="level">日志级别。</param>
        /// <returns>返回当前查询模型实例。</returns>
        public QueryModel WithLevel(LogLevel? level)
        {
            Level = level;
            return this;
        }

        /// <summary>
        /// 【已废弃】请使用 WithNextCursorTick。设置当前页的游时间戳（用于下一页/上一页查询）。
        /// </summary>
        /// <param name="nextCursorTick">当前页的游时间戳。</param>
        /// <returns>返回当前查询模型实例。</returns>
        [Obsolete("请使用 WithNextCursorTick 替代 WithCursorTick，此方法已废弃。", true)]
        public QueryModel WithCursorTick(long? nextCursorTick)
        {
            NextCursorTick = nextCursorTick;
            PrevCursorTick = null;
            return this;
        }

        /// <summary>
        /// 设置下一页的游标时间戳，用于分页查询时定位下一页数据的起始位置。
        /// 调用此方法后，PrevCursorTick 会被清空，仅用于下一页查询场景。
        /// </summary>
        /// <param name="nextCursorTick">
        /// 下一页的游标时间戳（通常为当前页最后一条日志的 CreatedTick）。
        /// </param>
        /// <returns>返回当前查询模型实例。</returns>
        public QueryModel WithNextCursorTick(long? nextCursorTick)
        {
            NextCursorTick = nextCursorTick;
            PrevCursorTick = null;
            return this;
        }

        /// <summary>
        /// 设置上一页的游标时间戳，用于分页查询时定位上一页数据的起始位置。
        /// 调用此方法后，NextCursorTick 会被清空，仅用于上一页查询场景。
        /// </summary>
        /// <param name="prevCursorTick">
        /// 上一页的游标时间戳（通常为当前页第一条日志的 CreatedTick）。
        /// </param>
        /// <returns>返回当前查询模型实例。</returns>
        public QueryModel WithPrevCursorTick(long? prevCursorTick)
        {
            PrevCursorTick = prevCursorTick;
            NextCursorTick = null;
            return this;
        }

        /// <summary>
        /// 设置日志时间范围（要求同一天且 StartTime &lt;= EndTime）。
        /// </summary>
        /// <param name="start">起始时间。</param>
        /// <param name="end">结束时间。</param>
        /// <returns>返回当前查询模型实例。</returns>
        public QueryModel WithTime(DateTime start, DateTime end)
        {
            if (start.Date != end.Date)
                return this;
            if (start > end)
                return this;

            StartTime = start;
            EndTime = end;
            return this;
        }
    }
}