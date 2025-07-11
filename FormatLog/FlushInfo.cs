namespace FormatLog
{
    /// <summary>
    /// 表示一次日志批量写入（Flush）操作的统计信息，包括数据准备耗时、写入耗时和日志数量。
    /// </summary>
    public class FlushInfo
    {
        /// <summary>
        /// 本次日志批量写入（Flush）操作对应的日期。
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// 数据准备阶段耗时（毫秒）。
        /// </summary>
        public double DataPreparationTime { get; set; }

        /// <summary>
        /// 数据写入数据库阶段耗时（毫秒）。
        /// </summary>
        public double DataWriteTime { get; set; }

        /// <summary>
        /// 本次日志批量写入（Flush）操作的总耗时（毫秒），包括数据准备和写入阶段。
        /// </summary>
        public double TotalTime { get; set; }

        /// <summary>
        /// 本次写入的日志数量。
        /// </summary>
        public int LogCount { get; set; }
    }
}