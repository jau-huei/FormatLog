﻿namespace FormatLog
{
    /// <summary>
    /// 游标分页查询结果模型。用于返回当前页数据及上一页、下一页查询所需的游标信息，支持高效的数据库双向分页检索。
    /// </summary>
    /// <typeparam name="T">数据项类型。</typeparam>
    public class KeysetPage<T>
    {
        /// <summary>
        /// 获取或设置当前页的数据集合。
        /// </summary>
        public List<T> Items { get; set; } = new();

        /// <summary>
        /// 游标分页：上一页的游时间戳（用于支持上一页查询）。
        /// </summary>
        public long? PreCursorTick { get; set; }

        /// <summary>
        /// 游标分页：下一页的游时间戳（用于支持下一页查询）。
        /// </summary>
        public long? NextCursorTick { get; set; }

        /// <summary>
        /// 全部数据总数（不受当前查询条件影响）。
        /// </summary>
        public long TotalRecords { get; set; }
    }
}