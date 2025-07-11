namespace FormatLog
{
    /// <summary>
    /// ͨ�����ݿ�ʵ��ӿڣ��ṩ�������� SQL ͷ���͵��� VALUES Ƭ�Ρ�
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// ��ȡ�������� SQL ͷ�����磺INSERT INTO ...����
        /// </summary>
        string GetInsertSql();

        /// <summary>
        /// ��ȡ��ǰʵ��� SQL VALUES Ƭ�Σ��磺('xxx')����
        /// </summary>
        string ToValueSql();
    }
}
