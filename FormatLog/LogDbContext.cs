using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;

namespace FormatLog
{
    /// <summary>
    /// 日志数据库上下文，管理日志相关实体的持久化。
    /// </summary>
    public class LogDbContext : DbContext
    {
        /// <summary>
        /// 日志表。
        /// </summary>
        public DbSet<Log> Logs { get; set; } = null!;

        /// <summary>
        /// 日志格式表。
        /// </summary>
        public DbSet<Format> Formats { get; set; } = null!;

        /// <summary>
        /// 调用者信息表。
        /// </summary>
        public DbSet<CallerInfo> CallerInfos { get; set; } = null!;

        /// <summary>
        /// 日志参数表。
        /// </summary>
        public DbSet<Argument> Arguments { get; set; } = null!;

        /// <summary>
        /// 日志区间统计表。
        /// </summary>
        public DbSet<LogIntervalStat> LogIntervalStats { get; set; } = null!;

        /// <summary>
        /// 当前数据库文件名。
        /// </summary>
        public string DbName { get; }

        /// <summary>
        /// 日志数据库文件存储目录。
        /// </summary>
        public static string DbDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "DB", "Log");

        /// <summary>
        /// 使用指定日期初始化日志数据库上下文。
        /// </summary>
        /// <param name="date">日期。</param>
        public LogDbContext(DateOnly date)
        {
            DbName = $"{date:yyyy_MM_dd}.sqlite";
        }

        /// <summary>
        /// 使用指定年月日初始化日志数据库上下文。
        /// </summary>
        /// <param name="year">年。</param>
        /// <param name="month">月。</param>
        /// <param name="day">日。</param>
        public LogDbContext(int year, int month, int day) : this(new DateOnly(year, month, day))
        {
        }

        /// <summary>
        /// 配置数据库连接。
        /// </summary>
        /// <param name="optionsBuilder">选项构建器。</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dbPath = Path.Combine(DbDirectory, DbName);
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}
