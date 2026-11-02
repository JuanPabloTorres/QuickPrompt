using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace QuickPrompt.Cloud.Delegation;

/// <summary>Persistent asymmetric credentials, supplied outside the repository; never modifies certificate trust.</summary>
public sealed class DelegationKeys(RSA signing, RSA encryption) : IDisposable
{
    public RsaSecurityKey Signing { get; } = Key(signing);
    public RsaSecurityKey Encryption { get; } = Key(encryption);
    private static RsaSecurityKey Key(RSA rsa) => new(rsa) { KeyId = Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())) };
    public static DelegationKeys Load(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Configure the private delegation key directory before enabling HTTPS delegation.");
        RSA Read(string name)
        {
            var key = RSA.Create();
            try
            {
                key.ImportFromPem(File.ReadAllText(Path.Combine(directory, name)));
                if (key.KeySize < 3072 || key.ExportParameters(true).D is null) throw new InvalidOperationException("Invalid delegation key.");
                return key;
            }
            catch { key.Dispose(); throw; }
        }
        var first = Read("signing.pem");
        try { return new(first, Read("encryption.pem")); } catch { first.Dispose(); throw; }
    }
    public void Dispose() { signing.Dispose(); encryption.Dispose(); }
}
