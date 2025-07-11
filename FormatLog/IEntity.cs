namespace FormatLog
{
    /// <summary>
    /// 通用数据库实体接口，提供批量插入 SQL 头部和单行 VALUES 片段。
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// 获取批量插入 SQL 头部（如：INSERT INTO ...）。
        /// </summary>
        string GetInsertSql();

        /// <summary>
        /// 获取当前实体的 SQL VALUES 片段（如：('xxx')）。
        /// </summary>
        string ToValueSql();
    }
}
