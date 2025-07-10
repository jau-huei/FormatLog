namespace FormatLog
{
    /// <summary>
    /// 提供字符串哈希计算方法，确保相同字符串在所有线程和运行环境下哈希值一致。
    /// </summary>
    public static class StringHash
    {
        /// <summary>
        /// 计算字符串的稳定哈希值（FNV-1a 32位实现）。
        /// </summary>
        /// <param name="input">要计算哈希的字符串。</param>
        /// <returns>哈希值。</returns>
        public static int GetStableHash(this string? input)
        {
            if (input == null)
                return 0;

            unchecked
            {
                const int fnvPrime = 16777619;
                int hash = (int)2166136261;
                foreach (char c in input)
                {
                    hash ^= c;
                    hash *= fnvPrime;
                }
                return hash;
            }
        }
    }
}