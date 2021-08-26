using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Commands
{
    public class FileHandler
    {
        public static byte[] downloadFile(string path)
        {

            return new byte[1];
        }

        public static string uploadFile(string path, byte[] file)
        {
            try
            {
                File.WriteAllBytes(path, file);
                return $"File successfully written to {path}";
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
