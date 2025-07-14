using Microsoft.EntityFrameworkCore;

namespace FormatLog
{
    /// <summary>
    /// 表示日志参数。
    /// </summary>
    [Index(nameof(Value), IsUnique = true)]
    public class Argument : ISqlInsertable
    {
        /// <summary>
        /// 获取或设置参数的唯一标识。
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 获取或设置参数值。
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// 初始化 <see cref="Argument"/> 类的新实例。
        /// </summary>
        public Argument() { }

        /// <summary>
        /// 使用指定的值初始化 <see cref="Argument"/> 类的新实例。
        /// </summary>
        /// <param name="value">参数值。</param>
        public Argument(object? value)
        {
            Value = value?.ToString();
        }

        /// <summary>
        /// 返回参数值的字符串表示。
        /// </summary>
        /// <returns>参数值字符串。</returns>
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        /// <summary>
        /// 判断当前参数与另一个参数是否相等。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>如果参数值相等则为 true，否则为 false。</returns>
        public override bool Equals(object? obj)
        {
            return obj is Argument other &&
                   Value == other.Value;
        }

        /// <summary>
        /// 获取参数值的稳定哈希码。
        /// </summary>
        /// <returns>哈希码。</returns>
        public override int GetHashCode()
        {
            return Value.GetStableHash();
        }

        /// <summary>
        /// 获取插入参数的 SQL 语句。
        /// </summary>
        /// <returns>插入参数的 SQL 语句。</returns>
        public string GetInsertSql() => "INSERT OR IGNORE INTO Arguments (Value) VALUES ";

        /// <summary>
        /// 将参数值转换为 SQL 表示。
        /// </summary>
        /// <returns>参数值的 SQL 表示。</returns>
        public string ToValueSql() => $"({(Value == null ? "NULL" : $"'{Value.Replace("'", "''")}'")})";
    }
}