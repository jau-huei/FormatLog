namespace FormatLog
{
    /// <summary>
    /// 日志排序方式。
    /// </summary>
    public enum OrderType
    {
        /// <summary>
        /// 按时间升序排序。
        /// </summary>
        OrderByTimeAscending,

        /// <summary>
        /// 按时间降序排序。
        /// </summary>
        OrderByTimeDescending,

        /// <summary>
        /// 按日志级别升序排序。
        /// </summary>
        OrderByLevelAscending,

        /// <summary>
        /// 按日志级别降序排序。
        /// </summary>
        OrderByLevelDescending,
    }
}