using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Roam.Api.Security;

/// <summary>Loads the certificate that encrypts the Data Protection key ring at rest.</summary>
public static class DataProtectionCertificate
{
    /// <summary>Returns null when no certificate is configured; throws when one is configured but unusable.</summary>
    public static X509Certificate2? Load(string? base64Pfx, string? password)
    {
        if (string.IsNullOrWhiteSpace(base64Pfx))
            return null;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64Pfx.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("DATA_PROTECTION_CERT_BASE64 is not valid base64.", ex);
        }

        X509Certificate2 certificate;
        try
        {
            // EphemeralKeySet keeps the private key in memory only (no key files written in the container).
            certificate = X509CertificateLoader.LoadPkcs12(bytes, password, X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("DATA_PROTECTION_CERT_BASE64 could not be loaded (wrong DATA_PROTECTION_CERT_PASSWORD?).", ex);
        }

        if (!certificate.HasPrivateKey)
            throw new InvalidOperationException("The Data Protection certificate has no private key, so keys could not be decrypted.");

        return certificate;
    }
}
