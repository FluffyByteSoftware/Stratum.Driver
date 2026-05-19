/*
 * (SessionTokenIssuer.cs)
 *------------------------------------------------------------
 * Created - 5/19/2026 8:18:50 AM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using Stratum.SystemTools.Logger;
using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Stratum.SystemTools.Security;

/// <summary>
/// Provides functionality for issuing and validating HMAC-SHA256 signed session tokens containing account identifiers
/// and expiration timestamps.
/// </summary>
/// <remarks>This class must be initialized with a cryptographic key using <see cref="Initialize"/> before issuing
/// or validating tokens. Tokens are Base64-URL encoded and contain an account identifier, issue timestamp, and
/// expiration timestamp, all protected by an HMAC-SHA256 signature.</remarks>
public static class SessionTokenIssuer
{
    private const int HmacSize = 32;
    private const int TimestampSize = 8;
    private const int AccountIdLenSize = 1;
    private const int MaxAccountIdBytes = 255;

    private static byte[]? _key;

    /// <summary>
    /// Initializes the session key with the specified byte array.
    /// </summary>
    /// <param name="key">The session key byte array.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> length does not match the required key size.</exception>
    public static void Initialize(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if(key.Length != SessionKeyProvider.KeySize)
        {
            throw new ArgumentException(
                $"Session key must be {SessionKeyProvider.KeySize} bytes, " +
                $"got {key.Length}.", nameof(key));
        }

        _key = key;
    }

    /// <summary>
    /// Issues a signed session token for the specified account with the given lifetime.
    /// </summary>
    /// <param name="accountId">The account identifier to include in the token.</param>
    /// <param name="lifetime">The duration for which the token is valid.</param>
    /// <returns>A Base64-URL-encoded signed session token containing the account identifier, issue timestamp, and expiration
    /// timestamp.</returns>
    /// <exception cref="InvalidOperationException">Initialize has not been called before issuing a token.</exception>
    /// <exception cref="ArgumentException">The UTF-8 byte count of <paramref name="accountId"/> exceeds the maximum allowed size.</exception>
    public static string Issue(string accountId, TimeSpan lifetime)
    {
        var key = _key ?? throw new InvalidOperationException(
            "SessionTokenIssuer.Initialize must be called before Issue.");

        var accountIdByteCount = Encoding.UTF8.GetByteCount(accountId);

        if(accountIdByteCount > MaxAccountIdBytes)
        {
            throw new ArgumentException(
                $"Account ID exceeds {MaxAccountIdBytes} UTFR-8 bytes.",
                nameof(accountId));
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var expireMs = nowMs + (long)lifetime.TotalMilliseconds;

        var payloadSize = AccountIdLenSize + accountIdByteCount + TimestampSize + TimestampSize;
        var totalSize = payloadSize + HmacSize;

        var buffer = ArrayPool<byte>.Shared.Rent(totalSize);

        try
        {
            var span = buffer.AsSpan(0, totalSize);
            span[0] = (byte)accountIdByteCount;

            Encoding.UTF8.GetBytes(accountId, span.Slice(AccountIdLenSize, accountIdByteCount));

            var tsOffset = AccountIdLenSize + accountIdByteCount;
            
            BinaryPrimitives.WriteInt64BigEndian(
                span.Slice(tsOffset, TimestampSize), nowMs);
            
            BinaryPrimitives.WriteInt64BigEndian(
                span.Slice(tsOffset + TimestampSize, TimestampSize),
                expireMs);

            Span<byte> tag = stackalloc byte[HmacSize];
            HMACSHA256.HashData(key, span[..payloadSize], tag);
            tag.CopyTo(span.Slice(payloadSize, HmacSize));

            return Base64UrlEncode(span);
        }
        catch(Exception ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Warn,
                $"Exception during issuance of certificate.", ex));

            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Validates a session token and extracts the account identifier and expiration timestamp.
    /// </summary>
    /// <param name="token">The session token to validate.</param>
    /// <param name="accountId">When this method returns, contains the account identifier if the token is valid; otherwise, an empty string.</param>
    /// <param name="expiresAt">When this method returns, contains the expiration timestamp if the token is valid; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if the token is valid and not expired; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">The issuer has not been initialized.</exception>
    public static bool TryValidate(
          string token,
          out string accountId,
          out DateTimeOffset expiresAt)
    {
        accountId = string.Empty;
        expiresAt = default;

        var key = _key ?? throw new InvalidOperationException(
            "SessionTokenIssuer.Initialize must be called before " +
            "TryValidate.");

        if (string.IsNullOrEmpty(token)) return false;

        var maxDecoded = (token.Length * 3 / 4) + 4;
        var buffer = ArrayPool<byte>.Shared.Rent(maxDecoded);
        try
        {
            if (!TryBase64UrlDecode(token, buffer, out var decodedLen))
            {
                return false;
            }

            var span = buffer.AsSpan(0, decodedLen);
            var minSize = AccountIdLenSize + TimestampSize
                + TimestampSize + HmacSize;
            if (span.Length < minSize) return false;

            int idLen = span[0];
            var payloadSize = AccountIdLenSize + idLen
                + TimestampSize + TimestampSize;
            if (span.Length != payloadSize + HmacSize) return false;

            Span<byte> expectedTag = stackalloc byte[HmacSize];
            HMACSHA256.HashData(key, span[..payloadSize], expectedTag);

            if (!CryptographicOperations.FixedTimeEquals(
                expectedTag, span.Slice(payloadSize, HmacSize)))
            {
                return false;
            }

            var tsOffset = AccountIdLenSize + idLen;
            var expiresMs = BinaryPrimitives.ReadInt64BigEndian(
                span.Slice(tsOffset + TimestampSize, TimestampSize));
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (nowMs > expiresMs) return false;

            accountId = Encoding.UTF8.GetString(
                span.Slice(AccountIdLenSize, idLen));
            expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(expiresMs);
            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }


    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var standard = Convert.ToBase64String(bytes);

        return standard
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static bool TryBase64UrlDecode(
        string input,
        Span<byte> destination,
        out int written)
    {
        written = 0;
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch(padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
            case 1: return false;
        }

        try
        {
            return Convert.TryFromBase64String(padded, destination, out written);
        }
        catch
        {
            return false;
        }
    }
}



/*
 *------------------------------------------------------------
 * (SessionTokenIssuer.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */