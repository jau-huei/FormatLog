namespace FormatLog
{
    /// <summary>
    /// 分页查询结果模型。
    /// </summary>
    /// <typeparam name="T">数据项类型。</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// 获取或设置总记录数。
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 获取总页数。
        /// </summary>
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        /// <summary>
        /// 获取或设置当前页索引(从 1 开始)。
        /// </summary>
        public int PageIndex { get; set; }

        /// <summary>
        /// 获取或设置每页数量。
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// 获取或设置当前页的数据集合。
        /// </summary>
        public List<T> Items { get; set; } = new();
    }
}