using Microsoft.EntityFrameworkCore;

namespace FormatLog
{
    /// <summary>
    /// 表示日志格式。
    /// </summary>
    [Index(nameof(FormatString), IsUnique = true)]
    public class Format : ISqlInsertable
    {
        /// <summary>
        /// 获取或设置格式的唯一标识。
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 获取或设置格式字符串。
        /// </summary>
        public string FormatString { get; set; } = string.Empty;

        /// <summary>
        /// 初始化 <see cref="Format"/> 类的新实例。
        /// </summary>
        public Format() { }

        /// <summary>
        /// 使用指定的格式字符串初始化 <see cref="Format"/> 类的新实例。
        /// </summary>
        /// <param name="formatString">格式字符串。</param>
        /// <exception cref="ArgumentException">当格式字符串为 null 或空时抛出。</exception>
        public Format(string formatString)
        {
            if (string.IsNullOrWhiteSpace(formatString))
            {
                throw new ArgumentException("格式字符串不能为空。", nameof(formatString));
            }
            FormatString = formatString;
        }

        /// <summary>
        /// 返回格式字符串的字符串表示。
        /// </summary>
        public override string ToString()
        {
            return FormatString;
        }

        /// <summary>
        /// 判断当前格式对象与另一个对象是否相等。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>如果对象相等则为 true，否则为 false。</returns>
        public override bool Equals(object? obj)
        {
            return obj is Format other &&
                   FormatString == other.FormatString;
        }

        /// <summary>
        /// 获取格式字符串的稳定哈希码。
        /// </summary>
        /// <returns>哈希码。</returns>
        public override int GetHashCode()
        {
            return FormatString.GetStableHash();
        }

        /// <summary>
        /// 获取插入 SQL 语句。
        /// </summary>
        /// <returns>插入 SQL 语句。</returns>
        public string GetInsertSql() => "INSERT OR IGNORE INTO Formats (FormatString) VALUES ";

        /// <summary>
        /// 转换为值的 SQL 表示。
        /// </summary>
        /// <returns>值的 SQL 表示。</returns>
        public string ToValueSql() => $"('{FormatString.Replace("'", "''")}')";
    }
}
