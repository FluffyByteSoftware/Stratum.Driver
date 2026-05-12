/*
 * (CertificateProvider.cs)
 *------------------------------------------------------------
 * Created - 5/11/2026 8:41:34 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using Stratum.SystemTools.Logger;
using Stratum.SystemTools.Storage;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Stratum.SystemTools.Security;

/// <summary>
/// Provides methods for loading or creating the application's server certificate used for secure communications.
/// </summary>
/// <remarks>This static class manages the server's X.509 certificate lifecycle. It attempts to load an existing
/// certificate from disk, and if none is found, generates a new self-signed ECDSA P-256 certificate and persists it for
/// future use. The certificate is intended for use in scenarios requiring server authentication, such as TLS
/// endpoints.</remarks>
public static class CertificateProvider
{
    private const string PfxRelativePath = "certs/server.pfx";
    private const string CerRelativePath = "certs/server.cert";
    private const string SubjectName = "CN=Stratum LoginServer";

    /// <summary>
    /// Loads an existing server certificate if available; otherwise, generates and persists a new self-signed ECDSA
    /// P-256 certificate.
    /// </summary>
    /// <remarks>If no existing certificate is found, a new self-signed certificate is created and persisted
    /// for future use. The method logs informational messages about the certificate loading or creation
    /// process.</remarks>
    /// <returns>An <see cref="X509Certificate2"/> representing the loaded or newly generated server certificate.</returns>
    public static X509Certificate2 LoadOrCreate()
    {
        if (TryLoadExisting(out var existing)) 
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Info,
                $"Loaded existing server certificate (expires {existing!.NotAfter:u})"));

            return existing;
        }

        Scribe.Pump(new ScribeMessage(ScribeSeverity.Info,
            $"No server certificate found; generating a new self-signed ECDSA P-256 cert..."));

        var generated = Generate();

        Persist(generated);

        return generated;
    }

    private static bool TryLoadExisting(out X509Certificate2? cert)
    {
        cert = null;

        byte[] pfxBytes;

        try
        {
            pfxBytes = DiskManager.Instance.ReadBinFile(PfxRelativePath);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch(Exception ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Error,
                $"Error loading existing server certificate", ex));
            return false;
        }

        cert = X509CertificateLoader.LoadPkcs12(
            pfxBytes,
            password: null,
            keyStorageFlags: X509KeyStorageFlags.Exportable);

        return true;
    }

    private static X509Certificate2 Generate()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var request = new CertificateRequest(
            SubjectName,
            ecdsa,
            HashAlgorithmName.SHA256);

        var sanBuilder = new SubjectAlternativeNameBuilder();

        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName("stratumdriver.duckdns.org");

        request.CertificateExtensions.Add(sanBuilder.Build());

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")],
                critical: false));

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = DateTimeOffset.UtcNow.AddYears(1);

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static void Persist(X509Certificate2 cert)
    {
        var pfxBytes = cert.Export(X509ContentType.Pkcs12);
        DiskManager.Instance.WriteBinFile(PfxRelativePath, pfxBytes);

        var cerBytes = cert.Export(X509ContentType.Cert);
        DiskManager.Instance.WriteBinFile(CerRelativePath, cerBytes);

        Scribe.Pump(new ScribeMessage(ScribeSeverity.Info,
            $"Wrote server certificate to {PfxRelativePath} and {CerRelativePath}"));
    }

}



/*
 *------------------------------------------------------------
 * (CertificateProvider.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */