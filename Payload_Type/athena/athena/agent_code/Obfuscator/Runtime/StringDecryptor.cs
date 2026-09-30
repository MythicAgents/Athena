namespace __OBFS_NS__
{
    internal static class __OBFS_CLASS__
    {
        internal static string __OBFS_METHOD__(byte[] data, byte key)
        {
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                result[i] = (byte)(data[i] ^ key);
            return System.Text.Encoding.UTF8.GetString(result);
        }
    }
}
