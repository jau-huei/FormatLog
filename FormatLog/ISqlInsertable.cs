namespace FormatLog
{
    /// <summary>
    /// 提供批量插入 SQL 语句和 VALUES 片段的接口。
    /// </summary>
    public interface ISqlInsertable
    {
        /// <summary>
        /// 获取插入 SQL 语句头部（如 INSERT INTO ...）。
        /// </summary>
        string GetInsertSql();

        /// <summary>
        /// 获取当前实体的 SQL VALUES 片段（如 ('xxx')）。
        /// </summary>
        string ToValueSql();
    }
}
