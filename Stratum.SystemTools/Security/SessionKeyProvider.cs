/*
 * (SessionKeyProvider.cs)
 *------------------------------------------------------------
 * Created - 5/19/2026 8:14:58 AM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using Stratum.SystemTools.Storage;
using System.Security.Cryptography;

namespace Stratum.SystemTools.Security;

public static class SessionKeyProvider
{
    public const int KeySize = 32;
    private const string KeyRelativePath = "keys/session_token.key";

    public static async Task<byte[]> LoadOrCreateAsync()
    {
        var disk = DiskManager.Instance;
        var existing = disk.ReadBinFile(KeyRelativePath);

        if (existing is not null && existing.Length == KeySize)
        {
            return existing;
        }

        if(existing is not null && existing.Length != KeySize)
        {
            throw new InvalidDataException(
                $"Session key file '{KeyRelativePath}' exists but is " +
                $"{existing.Length} bytes (expected {KeySize}).  Refusing " +
                $"to overwrite - delete the file manually if regeneration " +
                $"is desired.");
        }

        var fresh = new byte[KeySize];
        RandomNumberGenerator.Fill(fresh);

        disk.WriteBinFile(KeyRelativePath, fresh);
        
        return fresh;
    }
}



/*
 *------------------------------------------------------------
 * (SessionKeyProvider.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */