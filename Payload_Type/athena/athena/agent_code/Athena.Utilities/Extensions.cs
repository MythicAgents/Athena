using System;
using Athena.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Utilities
{
    public static class StringExtensions
    {
        public static bool IsEqualTo(this string str, string other)
        {
            if(str.ToHash() == other)
            {
                return true;
            }

            return false;
        }
        public static string ToHash(this string str)
        {
            return Misc.CreateMD5(str.ToLower());
        }
    }
}
