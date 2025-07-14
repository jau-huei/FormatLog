using FormatLog;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DemoWPF
{
    /// <summary>
    /// 表示用于日志列表显示的视图模型。用于将 <see cref="Log"/> 实例转换为适合界面展示的格式，
    /// 包含日志的创建时间、级别、内容和调用上下文信息。
    /// </summary>
    public class LogViewModel
    {
        /// <summary>
        /// 解析内容字符串为分段集合，根据 <tag> 标签分割并设置颜色。
        /// </summary>
        /// <param name="content">日志内容字符串。</param>
        /// <returns>分段集合。</returns>
        private List<LogTextSegment> ParseSegments(string content)
        {
            var segments = new List<LogTextSegment>();
            int idx = 0;
            while (idx < content.Length)
            {
                int tagStart = content.IndexOf("<tag>", idx, StringComparison.OrdinalIgnoreCase);
                if (tagStart < 0)
                {
                    segments.Add(new LogTextSegment { Text = content.Substring(idx) });
                    break;
                }
                if (tagStart > idx)
                {
                    segments.Add(new LogTextSegment { Text = content.Substring(idx, tagStart - idx) });
                }
                int tagEnd = content.IndexOf("</tag>", tagStart, StringComparison.OrdinalIgnoreCase);
                if (tagEnd < 0)
                {
                    segments.Add(new LogTextSegment { Text = content.Substring(tagStart), InTag = true });
                    break;
                }
                int tagContentStart = tagStart + 5;
                segments.Add(new LogTextSegment { Text = content.Substring(tagContentStart, tagEnd - tagContentStart), InTag = true });
                idx = tagEnd + 6;
            }
            return segments;
        }

        /// <summary>
        /// 使用指定的 <see cref="Log"/> 实例初始化 <see cref="LogViewModel"/>。
        /// </summary>
        /// <param name="log">日志实体。</param>
        public LogViewModel(Log log)
        {
            Id = log.Id;
            CreatedAt = log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Level = log.Level.ToString();
            Content = log.TagContent;
            Segments = ParseSegments(Content);
            CallerInfoString = log.CallerInfo?.ToString() ?? "";
        }

        /// <summary>
        /// 调用上下文信息字符串。
        /// </summary>
        public string CallerInfoString { get; }

        /// <summary>
        /// 日志内容。
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// 日志创建时间（格式：yyyy-MM-dd HH:mm:ss.fff）。
        /// </summary>
        public string CreatedAt { get; }

        /// <summary>
        /// 日志ID。
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 日志级别。
        /// </summary>
        public string Level { get; }

        /// <summary>
        /// 日志内容分段集合，每个分段包含文本和颜色。
        /// </summary>
        public List<LogTextSegment> Segments { get; } = new();

        /// <summary>
        /// 高性能渲染 Inlines 属性。
        /// </summary>
        public IEnumerable<Inline> Inlines
        {
            get
            {
                var black = new SolidColorBrush((Color)ColorConverter.ConvertFromString("Black"));
                foreach (var seg in Segments)
                {
                    var run = new Run(seg.Text);
                    if (seg.InTag)
                    {
                        run.FontWeight = FontWeights.Bold;
                        run.Foreground = black;
                    }
                    yield return run;
                }
            }
        }
    }
}