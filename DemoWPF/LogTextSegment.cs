namespace DemoWPF
{
    /// <summary>
    /// 表示日志内容的分段，每个分段包含文本和显示颜色。
    /// </summary>
    public class LogTextSegment
    {
        /// <summary>
        /// 分段文本内容。
        /// </summary>
        public string Text { get; set; } = "";

        /// <summary>
        /// 此段文本是否在标签内。
        /// </summary>
        public bool InTag { get; set; }
    }
}