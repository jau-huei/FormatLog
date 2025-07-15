using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FormatLog
{
    /// <summary>
    /// 表示日志区间统计信息（每10分钟一个区间，统计日志数量）。
    /// </summary>
    [Index(nameof(IntervalStart), IsUnique = true)]
    public class LogIntervalStat
    {
        /// <summary>
        /// 区间起始时间（精确到10分钟）。
        /// </summary>
        [Key]
        public DateTime IntervalStart { get; set; }

        /// <summary>
        /// 该区间内的日志数量。
        /// </summary>
        public int LogCount { get; set; }
    }
}