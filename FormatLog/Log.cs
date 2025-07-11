using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace FormatLog
{
    /// <summary>
    /// 表示日志信息。
    /// </summary>
    [Index(nameof(Level), nameof(CreatedAt), nameof(FormatId), nameof(CallerInfoId), nameof(Arg0Id), nameof(Arg1Id), nameof(Arg2Id), nameof(Arg3Id), nameof(Arg4Id), nameof(Arg5Id), nameof(Arg6Id), nameof(Arg7Id), nameof(Arg8Id), nameof(Arg9Id))]
    public class Log : ISqlInsertable
    {
        /// <summary>
        /// 获取或设置日志的唯一标识。
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 获取或设置日志级别。
        /// </summary>
        public LogLevel Level { get; set; } = LogLevel.Info;

        /// <summary>
        /// 获取或设置日志格式。
        /// </summary>
        public Format Format { get; set; } = new();

        /// <summary>
        /// 获取或设置日志格式的主键 Id。
        /// </summary>
        public long FormatId { get; set; }

        /// <summary>
        /// 获取或设置调用上下文信息，包括成员名称、源文件路径和行号。
        /// </summary>
        public CallerInfo? CallerInfo { get; set; }

        /// <summary>
        /// 获取或设置调用上下文的主键 Id。
        /// </summary>
        public long? CallerInfoId { get; set; }

        /// <summary>
        /// 获取或设置第 0 个参数。
        /// </summary>
        public Argument? Arg0 { get; set; }

        /// <summary>
        /// 获取或设置第 0 个参数的主键 Id。
        /// </summary>
        public long? Arg0Id { get; set; }

        /// <summary>
        /// 获取或设置第 1 个参数。
        /// </summary>
        public Argument? Arg1 { get; set; }

        /// <summary>
        /// 获取或设置第 1 个参数的主键 Id。
        /// </summary>
        public long? Arg1Id { get; set; }

        /// <summary>
        /// 获取或设置第 2 个参数。
        /// </summary>
        public Argument? Arg2 { get; set; }

        /// <summary>
        /// 获取或设置第 2 个参数的主键 Id。
        /// </summary>
        public long? Arg2Id { get; set; }

        /// <summary>
        /// 获取或设置第 3 个参数。
        /// </summary>
        public Argument? Arg3 { get; set; }

        /// <summary>
        /// 获取或设置第 3 个参数的主键 Id。
        /// </summary>
        public long? Arg3Id { get; set; }

        /// <summary>
        /// 获取或设置第 4 个参数。
        /// </summary>
        public Argument? Arg4 { get; set; }

        /// <summary>
        /// 获取或设置第 4 个参数的主键 Id。
        /// </summary>
        public long? Arg4Id { get; set; }

        /// <summary>
        /// 获取或设置第 5 个参数。
        /// </summary>
        public Argument? Arg5 { get; set; }

        /// <summary>
        /// 获取或设置第 5 个参数的主键 Id。
        /// </summary>
        public long? Arg5Id { get; set; }

        /// <summary>
        /// 获取或设置第 6 个参数。
        /// </summary>
        public Argument? Arg6 { get; set; }

        /// <summary>
        /// 获取或设置第 6 个参数的主键 Id。
        /// </summary>
        public long? Arg6Id { get; set; }

        /// <summary>
        /// 获取或设置第 7 个参数。
        /// </summary>
        public Argument? Arg7 { get; set; }

        /// <summary>
        /// 获取或设置第 7 个参数的主键 Id。
        /// </summary>
        public long? Arg7Id { get; set; }

        /// <summary>
        /// 获取或设置第 8 个参数。
        /// </summary>
        public Argument? Arg8 { get; set; }

        /// <summary>
        /// 获取或设置第 8 个参数的主键 Id。
        /// </summary>
        public long? Arg8Id { get; set; }

        /// <summary>
        /// 获取或设置第 9 个参数。
        /// </summary>
        public Argument? Arg9 { get; set; }

        /// <summary>
        /// 获取或设置第 9 个参数的主键 Id。
        /// </summary>
        public long? Arg9Id { get; set; }

        /// <summary>
        /// 获取或设置日志创建时间。
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 初始化 <see cref="Log"/> 类的新实例。
        /// </summary>
        public Log() { }

        /// <summary>
        /// 使用指定格式字符串和参数初始化 <see cref="Log"/> 类的新实例，日志级别为 Info。
        /// </summary>
        /// <param name="format">格式字符串。</param>
        /// <param name="arguments">参数列表。</param>
        public Log(string format, params object[] arguments) : this(LogLevel.Info, format, arguments)
        {
        }

        /// <summary>
        /// 使用指定日志级别、格式字符串和参数初始化 <see cref="Log"/> 类的新实例。
        /// </summary>
        /// <param name="level">日志级别。</param>
        /// <param name="format">格式字符串。</param>
        /// <param name="arguments">参数列表。</param>
        public Log(LogLevel level, string format, params object[] arguments)
        {
            Level = level;
            Format = new Format(format);
            if (arguments != null && arguments.Length > 0)
            {
                for (int i = 0; i < arguments.Length; i++)
                {
                    SetArgument(i, arguments[i]);
                }
            }
        }

        /// <summary>
        /// 设置调用上下文信息。
        /// </summary>
        /// <param name="memberName">成员名称。</param>
        /// <param name="sourceFilePath">源文件路径。</param>
        /// <param name="sourceLineNumber">源文件行号。</param>
        /// <returns>带有调用上下文信息的 <see cref="Log"/> 实例。</returns>
        public Log WithCallerInfo([CallerMemberName] string? memberName = null, [CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        {
            if (string.IsNullOrEmpty(memberName) && string.IsNullOrEmpty(sourceFilePath) && sourceLineNumber < 0)
            {
                CallerInfo = null;
            }
            else
            {
                CallerInfo = new CallerInfo(memberName, sourceFilePath, sourceLineNumber);
            }

            return this;
        }

        /// <summary>
        /// 返回日志的字符串表示。
        /// </summary>
        /// <returns>日志字符串。</returns>
        public override string ToString()
        {
            var args = GetArgumentsAsObject();

            if (CallerInfo == null)
                return $"[#{Id}][{CreatedAt:yy-MM-dd HH:mm:ss.fff}] [{Level}] {string.Format(Format.FormatString, args)}";

            return $"[#{Id}][{CreatedAt:yy-MM-dd HH:mm:ss.fff}] [{Level}] {string.Format(Format.FormatString, args)} | {CallerInfo}";
        }

        /// <summary>
        /// 获取所有非空参数（Arg0~Arg9），以数组形式返回。
        /// </summary>
        /// <returns>所有不为 null 的 <see cref="Argument"/> 实例数组。</returns>
        public Argument[] GetNotNullArguments()
        {
            return GetArguments().Select(arg => arg!).Where(arg => arg != null).ToArray();
        }

        /// <summary>
        /// 获取所有参数的值数组（Arg0~Arg9）。
        /// </summary>
        /// <returns>参数值数组。</returns>
        public Argument?[] GetArguments()
        {
            return new Argument?[] { Arg0, Arg1, Arg2, Arg3, Arg4, Arg5, Arg6, Arg7, Arg8, Arg9 };
        }

        /// <summary>
        /// 获取所有参数的值数组（Arg0~Arg9）。
        /// </summary>
        /// <returns>参数值数组。</returns>
        public object?[] GetArgumentsAsObject()
        {
            return new object?[] { Arg0, Arg1, Arg2, Arg3, Arg4, Arg5, Arg6, Arg7, Arg8, Arg9 };
        }

        /// <summary>
        /// 按索引设置参数（Arg0~Arg9）。
        /// </summary>
        /// <param name="index">参数索引（0~9）。</param>
        /// <param name="argument">参数值。</param>
        public void SetArgument(int index, object? argument)
        {
            switch (index)
            {
                case 0:
                    Arg0 = new Argument(argument); break;
                case 1:
                    Arg1 = new Argument(argument); break;
                case 2:
                    Arg2 = new Argument(argument); break;
                case 3:
                    Arg3 = new Argument(argument); break;
                case 4:
                    Arg4 = new Argument(argument); break;
                case 5:
                    Arg5 = new Argument(argument); break;
                case 6:
                    Arg6 = new Argument(argument); break;
                case 7:
                    Arg7 = new Argument(argument); break;
                case 8:
                    Arg8 = new Argument(argument); break;
                case 9:
                    Arg9 = new Argument(argument); break;
                default:
                    throw new ArgumentException("参数数量范围错误。", nameof(index));
            }
        }

        /// <summary>
        /// 创建一个仅包含主键信息（Id、FormatId、CallerInfoId、Arg0Id~Arg9Id、Level、CreatedAt）的浅拷贝日志对象，
        /// 用于去除所有导航属性和参数对象，便于序列化、存储或简化传递。
        /// </summary>
        /// <returns>仅包含主键信息和基础字段的新 <see cref="Log"/> 实例。</returns>
        public Log ToKeyOnly()
        {
            return new Log()
            {
                Level = Level,
                FormatId = FormatId,
                CallerInfoId = CallerInfoId,
                Arg0Id = Arg0Id,
                Arg1Id = Arg1Id,
                Arg2Id = Arg2Id,
                Arg3Id = Arg3Id,
                Arg4Id = Arg4Id,
                Arg5Id = Arg5Id,
                Arg6Id = Arg6Id,
                Arg7Id = Arg7Id,
                Arg8Id = Arg8Id,
                Arg9Id = Arg9Id,
            };
        }

        /// <summary>
        /// 获取插入日志的 SQL 语句。
        /// </summary>
        /// <returns>插入日志的 SQL 语句。</returns>
        public string GetInsertSql() => "INSERT INTO Logs (Level, FormatId, CallerInfoId, Arg0Id, Arg1Id, Arg2Id, Arg3Id, Arg4Id, Arg5Id, Arg6Id, Arg7Id, Arg8Id, Arg9Id, CreatedAt) VALUES ";

        /// <summary>
        /// 将日志转换为 SQL 值字符串。
        /// </summary>
        /// <returns>日志的 SQL 值字符串。</returns>
        public string ToValueSql()
        {
            string val(long? v) => v.HasValue ? v.Value.ToString() : "NULL";
            return $"({(int)Level},{FormatId},{val(CallerInfoId)},{val(Arg0Id)},{val(Arg1Id)},{val(Arg2Id)},{val(Arg3Id)},{val(Arg4Id)},{val(Arg5Id)},{val(Arg6Id)},{val(Arg7Id)},{val(Arg8Id)},{val(Arg9Id)},'{CreatedAt:yyyy-MM-dd HH:mm:ss.fffffff}')";
        }
    }
}