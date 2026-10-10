using System.Security.Cryptography;
using System.Text;

namespace EngineeredTutors.Api.Security;

public sealed class SecurityOptions
{
    /// <summary>Base64, 32 bytes. Encrypts ID numbers and queued email bodies. From Key Vault in production.</summary>
    public string EncryptionKey { get; set; } = "";
    /// <summary>Base64, at least 32 bytes. Separate key for the ID number HMAC used in duplicate checks.</summary>
    public string IdHashKey { get; set; } = "";
}

/// <summary>
/// Application-level encryption (AES-256-GCM) and keyed hashing (HMAC-SHA256) for sensitive values, section 5.1.
/// </summary>
public sealed class Protector
{
    private readonly byte[] _encKey;
    private readonly byte[] _hashKey;

    public Protector(SecurityOptions o)
    {
        _encKey = DecodeKey(o.EncryptionKey, nameof(o.EncryptionKey), exact: 32);
        _hashKey = DecodeKey(o.IdHashKey, nameof(o.IdHashKey), exact: null);
        if (_encKey.AsSpan().SequenceEqual(_hashKey))
            throw new InvalidOperationException("Security:EncryptionKey and Security:IdHashKey must be different keys.");
    }

    private static byte[] DecodeKey(string b64, string name, int? exact)
    {
        if (string.IsNullOrWhiteSpace(b64))
            throw new InvalidOperationException($"Security:{name} is not configured. See api/.env.example.");
        byte[] key;
        try { key = Convert.FromBase64String(b64); }
        catch (FormatException) { throw new InvalidOperationException($"Security:{name} must be base64."); }
        if (exact is { } n ? key.Length != n : key.Length < 32)
            throw new InvalidOperationException($"Security:{name} must be {(exact is null ? "at least " : "")}32 bytes.");
        return key;
    }

    /// <summary>Returns base64(nonce ‖ tag ‖ ciphertext).</summary>
    public string Encrypt(string plaintext)
    {
        var data = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var cipher = new byte[data.Length];
        using (var aes = new AesGcm(_encKey, tag.Length))
            aes.Encrypt(nonce, data, cipher, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string Decrypt(string protectedValue)
    {
        var all = Convert.FromBase64String(protectedValue);
        var nonceLen = AesGcm.NonceByteSizes.MaxSize;
        var tagLen = AesGcm.TagByteSizes.MaxSize;
        var nonce = all.AsSpan(0, nonceLen);
        var tag = all.AsSpan(nonceLen, tagLen);
        var cipher = all.AsSpan(nonceLen + tagLen);
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(_encKey, tagLen))
            aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>HMAC-SHA256 of an already-normalised ID number, as lower-case hex.</summary>
    public string HashId(string normalisedId) =>
        Convert.ToHexString(HMACSHA256.HashData(_hashKey, Encoding.UTF8.GetBytes(normalisedId))).ToLowerInvariant();
}

/// <summary>Attempt access tokens, DX-01: 256 random bits, base64url; only the SHA-256 is stored.</summary>
public static class AttemptTokens
{
    public static string New() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>Rejects anything that can't be a token before touching the database.</summary>
    public static bool LooksValid(string? token) =>
        token is { Length: 43 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
