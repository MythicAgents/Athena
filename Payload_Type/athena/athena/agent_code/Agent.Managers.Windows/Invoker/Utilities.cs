using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Invoker.Dynamic;

public static class Utilities
{
    /// <summary>
    /// Generate an HMAC-MD5 hash of the supplied string using an Int64 as the key. This is useful for unique hash based API lookups.
    /// </summary>
    /// <author>Ruben Boonen (@FuzzySec)</author>
    /// <param name="value">String to hash.</param>
    /// <param name="key">64-bit integer to initialize the keyed hash object (e.g. 0xabc or 0x1122334455667788).</param>
    /// <returns>string, the computed MD5 hash value.</returns>
    public static string GetFuncHash(string value, long key)
    {
        var data = Encoding.UTF8.GetBytes(value.ToLower());
        var bytes = BitConverter.GetBytes(key);

        using var hmac = new HMACMD5(bytes);
        var bHash = hmac.ComputeHash(data);
        return BitConverter.ToString(bHash).Replace("-", "");
    }
}