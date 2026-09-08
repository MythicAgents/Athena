namespace Agent.Utilities;

public static class Base64Transfer
{
    public static int MaximumEncodedLength(int maximumDecodedLength)
    {
        if (maximumDecodedLength < 0) throw new ArgumentOutOfRangeException(nameof(maximumDecodedLength));
        long length = 4L * ((maximumDecodedLength + 2L) / 3L);
        if (length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(maximumDecodedLength));
        return (int)length;
    }

    public static byte[] Decode(string? encoded, int maximumDecodedLength)
    {
        if (maximumDecodedLength < 0) throw new ArgumentOutOfRangeException(nameof(maximumDecodedLength));
        encoded ??= string.Empty;
        if (encoded.Length > MaximumEncodedLength(maximumDecodedLength))
            throw new ArgumentException("Encoded data exceeds the configured size limit.", nameof(encoded));
        byte[] decoded = Convert.FromBase64String(encoded);
        if (decoded.Length > maximumDecodedLength)
            throw new ArgumentException("Decoded data exceeds the configured size limit.", nameof(encoded));
        return decoded;
    }
}
