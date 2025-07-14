using FormatLog;

namespace DemoWPF
{
    /// <summary>
    /// 表示用于日志列表显示的视图模型。用于将 <see cref="Log"/> 实例转换为适合界面展示的格式，
    /// 包含日志的创建时间、级别、内容和调用上下文信息。
    /// </summary>
    public class LogViewModel
    {
        /// <summary>
        /// 日志创建时间（格式：yyyy-MM-dd HH:mm:ss.fff）。
        /// </summary>
        public string CreatedAt { get; }
        /// <summary>
        /// 日志级别。
        /// </summary>
        public string Level { get; }
        /// <summary>
        /// 日志内容。
        /// </summary>
        public string Content { get; }
        /// <summary>
        /// 调用上下文信息字符串。
        /// </summary>
        public string CallerInfoString { get; }
        /// <summary>
        /// 使用指定的 <see cref="Log"/> 实例初始化 <see cref="LogViewModel"/>。
        /// </summary>
        /// <param name="log">日志实体。</param>
        public LogViewModel(Log log)
        {
            CreatedAt = log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Level = log.Level.ToString();
            Content = log.Content;
            CallerInfoString = log.CallerInfo?.ToString() ?? "";
        }
    }
}