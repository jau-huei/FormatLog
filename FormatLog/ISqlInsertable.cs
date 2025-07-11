namespace FormatLog
{
    /// <summary>
    /// �ṩ�������� SQL ���� VALUES Ƭ�εĽӿڡ�
    /// </summary>
    public interface ISqlInsertable
    {
        /// <summary>
        /// ��ȡ���� SQL ���ͷ������ INSERT INTO ...����
        /// </summary>
        string GetInsertSql();

        /// <summary>
        /// ��ȡ��ǰʵ��� SQL VALUES Ƭ�Σ��� ('xxx')����
        /// </summary>
        string ToValueSql();
    }
}
