using Microsoft.EntityFrameworkCore;

namespace FormatLog
{
    /// <summary>
    /// 表示记录日志时的调用上下文信息，包括成员名称、源文件路径和行号。
    /// </summary>
    [Index(nameof(MemberName), nameof(SourceFilePath), nameof(SourceLineNumber))]
    public class CallerInfo : IEntity
    {
        /// <summary>
        /// 获取或设置成员的唯一标识。
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 调用成员名称。
        /// </summary>
        public string? MemberName { get; set; }

        /// <summary>
        /// 源文件路径。
        /// </summary>
        public string? SourceFilePath { get; set; }

        /// <summary>
        /// 源文件行号。
        /// </summary>
        public int? SourceLineNumber { get; set; }

        /// <summary>
        /// 初始化 <see cref="CallerInfo"/> 类的新实例。
        /// </summary>
        public CallerInfo() { }

        /// <summary>
        /// 初始化 <see cref="CallerInfo"/> 实例，包含成员名、源文件路径和行号。
        /// </summary>
        /// <param name="memberName">成员名称。</param>
        /// <param name="sourceFilePath">源文件路径。</param>
        /// <param name="sourceLineNumber">源文件行号。</param>
        public CallerInfo(string? memberName, string? sourceFilePath, int? sourceLineNumber)
        {
            MemberName = memberName;
            SourceFilePath = sourceFilePath;
            SourceLineNumber = sourceLineNumber;
        }

        /// <summary>
        /// 返回成员的字符串表示。
        /// </summary>
        /// <returns>成员信息字符串。</returns>
        public override string ToString()
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(MemberName))
                parts.Add($"Member: {MemberName}");

            if (SourceLineNumber.HasValue)
                parts.Add($"Line: {SourceLineNumber.Value}");

            if (!string.IsNullOrEmpty(SourceFilePath))
                parts.Add($"File: {SourceFilePath}");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// 判断当前对象与另一个对象是否相等。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>如果对象相等则为 true，否则为 false。</returns>
        public override bool Equals(object? obj)
        {
            return obj is CallerInfo other &&
                   MemberName == other.MemberName &&
                   SourceFilePath == other.SourceFilePath &&
                   SourceLineNumber == other.SourceLineNumber;
        }

        /// <summary>
        /// 获取当前稳定哈希码。
        /// </summary>
        /// <returns>哈希码。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (MemberName?.GetStableHash() ?? 0);
                hash = hash * 31 + (SourceFilePath?.GetStableHash() ?? 0);
                hash = hash * 31 + (SourceLineNumber?.GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>
        /// 获取插入 SQL 语句。
        /// </summary>
        /// <returns>插入 SQL 语句。</returns>
        public string GetInsertSql() => "INSERT INTO CallerInfos (MemberName, SourceFilePath, SourceLineNumber) VALUES ";

        /// <summary>
        /// 转换为值的 SQL 表示。
        /// </summary>
        /// <returns>值的 SQL 表示。</returns>
        public string ToValueSql()
        {
            var member = MemberName == null ? "NULL" : $"'{MemberName.Replace("'", "''")}'";
            var file = SourceFilePath == null ? "NULL" : $"'{SourceFilePath.Replace("'", "''")}'";
            var line = SourceLineNumber.HasValue ? SourceLineNumber.Value.ToString() : "NULL";
            return $"({member}, {file}, {line})";
        }
    }
}