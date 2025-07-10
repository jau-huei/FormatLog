namespace FormatLog
{
    /// <summary>
    /// 日志查询参数模型。
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
        /// 获取或设置当前页索引(从 1 开始)。
        /// </summary>
        public int PageIndex { get; set; }

        /// <summary>
        /// 获取或设置每页显示的日志数量。
        /// </summary>
        public int PageSize { get; set; } = 20;

        /// <summary>
        /// 获取或设置日志创建时间范围的起始时间。
        /// </summary>
        public DateTime? StartTime { get; set; }

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
        public QueryModel WithArgs(string arguments)
        {
            Argument = arguments;
            return this;
        }

        /// <summary>
        /// 设置调用者信息的匹配内容，用于日志查询时筛选调用上下文。
        /// </summary>
        /// <param name="callerInfo">调用者信息（如成员名、文件路径等）。</param>
        /// <returns>返回当前查询模型实例。</returns>
        public QueryModel WithCaller(string callerInfo)
        {
            CallerInfo = callerInfo;
            return this;
        }

        /// <summary>
        /// 设置日志格式字符串的匹配内容。
        /// </summary>
        /// <param name="formatString">格式字符串。</param>
        /// <returns>返回当前查询模型实例。</returns>
        public QueryModel WithFormat(string formatString)
        {
            FormatString = formatString;
            return this;
        }

        /// <summary>
        /// 设置日志级别。
        /// </summary>
        /// <param name="level">日志级别。</param>
        /// <returns>返回当前查询模型实例。</returns>
        public QueryModel WithLevel(LogLevel level)
        {
            Level = level;
            return this;
        }

        /// <summary>
        /// 设置当前页索引。
        /// </summary>
        /// <param name="pageIndex">页索引（从 1 开始）。</param>
        /// <returns>返回当前查询模型实例。</returns>
        public QueryModel WithPageIndex(int pageIndex)
        {
            PageIndex = pageIndex;
            return this;
        }
    }
}