/*
 * (Ed25519Verifier.cs)
 *------------------------------------------------------------
 * Created - 5/19/2026 7:00:33 AM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Stratum.SystemTools.Logger;

namespace Stratum.SystemTools.Security;

public static class Ed25519Verifier
{
    /// <summary>
    /// The size of a public key in bytes.
    /// </summary>
    public const int PublicKeySize = 32;
    /// <summary>
    /// The size of a signature in bytes.
    /// </summary>
    public const int SignatureSize = 64;

    /// <summary>
    /// Verifies an Ed25519 signature against a message using the specified public key.
    /// </summary>
    /// <remarks>Returns <see langword="false"/> if the public key or signature length is invalid, or
    /// if verification fails due to an exception.</remarks>
    /// <param name="publicKey">The Ed25519 public key to use for verification.</param>
    /// <param name="message">The message that was signed.</param>
    /// <param name="signature">The Ed25519 signature to verify.</param>
    /// <returns><see langword="true"/> if the signature is valid; otherwise, <see langword="false"/>.</returns>
    public static bool Verify(
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> signature)
    {
        if (publicKey.Length != PublicKeySize) return false;
        if (signature.Length != SignatureSize) return false;

        try
        {
            var keyParams = new Ed25519PublicKeyParameters(
                publicKey.ToArray(), 0);

            var verifier = new Ed25519Signer();

            verifier.Init(false, keyParams);

            var messageBytes = message.ToArray();
            verifier.BlockUpdate(messageBytes, 0, messageBytes.Length);

            return verifier.VerifySignature(signature.ToArray());
        }
        catch(Exception ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Warn,
                $"Failed to verify Ed25519 signature.", ex));

            return false;
        }
    }
}

/*
*------------------------------------------------------------
* (Ed25519Verifier.cs)
* See License.txt for licensing information.
*-----------------------------------------------------------
*/