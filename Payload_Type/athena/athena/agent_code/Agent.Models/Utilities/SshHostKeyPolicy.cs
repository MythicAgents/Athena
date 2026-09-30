using System.Security.Cryptography;
using System.Text;

namespace Agent.Utilities
{
    public static class SshHostKeyPolicy
    {
        public static bool IsTrusted(string? configuredFingerprint, string sha256Fingerprint, string md5Fingerprint)
        {
            if (string.IsNullOrWhiteSpace(configuredFingerprint)) return false;

            string configured = configuredFingerprint.Trim();
            if (configured.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
                return FixedTimeEquals(configured[7..], sha256Fingerprint, ignoreCase: false);
            if (configured.StartsWith("MD5:", StringComparison.OrdinalIgnoreCase))
                return FixedTimeEquals(configured[4..], md5Fingerprint, ignoreCase: true);
            return FixedTimeEquals(configured, sha256Fingerprint, ignoreCase: false);
        }

        private static bool FixedTimeEquals(string expected, string actual, bool ignoreCase)
        {
            if (ignoreCase)
            {
                expected = expected.ToUpperInvariant();
                actual = actual.ToUpperInvariant();
            }
            byte[] expectedBytes = Encoding.ASCII.GetBytes(expected);
            byte[] actualBytes = Encoding.ASCII.GetBytes(actual);
            return expectedBytes.Length == actualBytes.Length &&
                CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }
    }
}
