/*
 * (CertificateProvider.cs)
 *------------------------------------------------------------
 * Created - 5/11/2026 8:41:34 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Stratum.SystemTools.Logger;

namespace Stratum.SystemTools.Security;

/// <summary>
/// Provides utilities for loading or creating self-signed X.509 certificates for TLS authentication.
/// </summary>
public static class CertificateProvider
{
    private const string SubjectName = "CN=Stratum LoginServer";
    private static readonly TimeSpan Validity = TimeSpan.FromDays(365 * 10);

    /// <summary>
    /// Loads an existing X509 certificate from the specified PFX file, or generates and persists a new certificate if
    /// the file does not exist.
    /// </summary>
    /// <param name="pfxPath">The path to the PFX certificate file.</param>
    /// <param name="cerPath">The path to the CER certificate file used when generating a new certificate.</param>
    /// <param name="sanHostnames">The Subject Alternative Name hostnames to include in the certificate.</param>
    /// <returns>The loaded or newly created X509 certificate.</returns>
    /// <exception cref="ArgumentException"><paramref name="pfxPath"/> is <see langword="null"/> or empty, <paramref name="cerPath"/> is <see
    /// langword="null"/> or empty, or <paramref name="sanHostnames"/> contains no elements.</exception>
    public static X509Certificate2 LoadOrCreate(
        string pfxPath,
        string cerPath,
        IReadOnlyList<string> sanHostnames)
    {
        ArgumentException.ThrowIfNullOrEmpty(pfxPath);
        ArgumentException.ThrowIfNullOrEmpty(cerPath);
        ArgumentNullException.ThrowIfNull(sanHostnames);
        if (sanHostnames.Count == 0)
            throw new ArgumentException("At least one SAN hostname is required.", nameof(sanHostnames));

        if (File.Exists(pfxPath))
            return LoadExisting(pfxPath);

        return GenerateAndPersist(pfxPath, cerPath, sanHostnames);
    }

    private static X509Certificate2 LoadExisting(string pfxPath)
    {
        X509Certificate2 cert;
        try
        {
            cert = X509CertificateLoader.LoadPkcs12FromFile(
                pfxPath,
                password: null,
                keyStorageFlags: X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
        }
        catch (Exception ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Error,
                $"Existing certificate at '{pfxPath}' could not be loaded.", ex));
            throw;
        }

        if (cert.NotAfter < DateTime.UtcNow)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Error,
                $"Certificate at '{pfxPath}' expired on {cert.NotAfter:u}. Delete the file to regenerate."));
            cert.Dispose();
            throw new InvalidOperationException("Certificate expired.");
        }

        Scribe.Pump(new ScribeMessage(ScribeSeverity.Info,
            $"Loaded TLS certificate (expires {cert.NotAfter:u})."));
        return cert;
    }

    private static X509Certificate2 GenerateAndPersist(
        string pfxPath,
        string cerPath,
        IReadOnlyList<string> sanHostnames)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(SubjectName, ecdsa, HashAlgorithmName.SHA256);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: false,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: true));

        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.KeyAgreement,
            critical: true));

        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.1")], // ServerAuthentication
            critical: true));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        foreach (var name in sanHostnames)
        {
            if (IPAddress.TryParse(name, out var ip))
                sanBuilder.AddIpAddress(ip);
            else
                sanBuilder.AddDnsName(name);
        }
        request.CertificateExtensions.Add(sanBuilder.Build());

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = notBefore + Validity;
        using var generated = request.CreateSelfSigned(notBefore, notAfter);

        var pfxBytes = generated.Export(X509ContentType.Pfx);
        var cerBytes = generated.Export(X509ContentType.Cert);

        EnsureDirectory(pfxPath);
        EnsureDirectory(cerPath);
        WriteAtomic(pfxPath, pfxBytes);
        WriteAtomic(cerPath, cerBytes);

        var loaded = X509CertificateLoader.LoadPkcs12(
            pfxBytes,
            password: null,
            keyStorageFlags: X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

        Scribe.Pump(new ScribeMessage(ScribeSeverity.Info,
            $"Generated TLS certificate at '{pfxPath}' (expires {loaded.NotAfter:u})."));
        return loaded;
    }

    private static void EnsureDirectory(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush(true);
        }

        if (File.Exists(path))
            File.Replace(tmp, path, destinationBackupFileName: null);
        else
            File.Move(tmp, path);
    }
}


/*
 *------------------------------------------------------------
 * (CertificateProvider.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */