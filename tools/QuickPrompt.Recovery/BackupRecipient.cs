using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace QuickPrompt.Recovery;

/// <summary>Checks a pinned document-encryption recipient without installing certificates or trusting a public PKI.</summary>
public static class BackupRecipient
{
    public const string DocumentEncryptionOid = "1.3.6.1.4.1.311.80.1";

    public static string Fingerprint(X509Certificate2 certificate) => certificate.GetCertHashString(HashAlgorithmName.SHA256);

    public static void RequireDedicated(X509Certificate2 certificate, string expectedSha256)
    {
        BackupArchive.RequireHash(expectedSha256);
        if (Fingerprint(certificate) != expectedSha256) throw new CryptographicException("The backup recipient fingerprint differs.");
        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is null || rsa.KeySize is < 2048 or > 16384) throw new CryptographicException("A 2048-bit or larger RSA backup recipient is required; 3072 bits is recommended.");
        var usages = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().ToArray();
        if (usages.Length != 1 || usages[0].EnhancedKeyUsages.Count != 1 || usages[0].EnhancedKeyUsages[0].Value != DocumentEncryptionOid)
            throw new CryptographicException("Use a dedicated document-encryption recipient, never a TLS, delegation or host-keyring certificate.");
        var keyUsage = certificate.Extensions.OfType<X509KeyUsageExtension>().ToArray();
        if (keyUsage.Length != 1 || !keyUsage[0].KeyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment)
            || certificate.Extensions.OfType<X509BasicConstraintsExtension>().Any(constraint => constraint.CertificateAuthority))
            throw new CryptographicException("The backup recipient must permit key encipherment and cannot be a certificate authority.");
    }
}
