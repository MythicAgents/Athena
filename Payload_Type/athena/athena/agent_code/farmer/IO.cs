using System.IO;

namespace Plugin
{
    class IO
    {
        public static void WriteToFile(string content)
        {
            if (Config.encrypt)
                Config.sw.WriteLine(Crypto.Encrypt(content, Config.key));
            else Config.sw.WriteLine(content);

        }
    }
}
