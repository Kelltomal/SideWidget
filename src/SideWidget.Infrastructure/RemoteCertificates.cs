using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SideWidget.Infrastructure;

[SupportedOSPlatform("windows")]
public sealed class RemoteCertificates : IDisposable
{
    readonly X509Certificate2 root;
    public RemoteCertificates(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "root.pfx.dpapi");
        if (File.Exists(path))
        {
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
            try { root = X509CertificateLoader.LoadPkcs12(bytes, null, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            if (!root.HasPrivateKey || root.NotAfter.ToUniversalTime() <= DateTime.UtcNow.AddDays(2))
                throw new InvalidOperationException("Сертификат пульта истёк. Требуется обновить доверие на телефоне.");
        }
        else
        {
            using var key = RSA.Create(3072);
            var request = new CertificateRequest("CN=SideWidget Home Remote " + Guid.NewGuid().ToString("N")[..8], key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
            var bytes = created.Export(X509ContentType.Pfx);
            try
            {
                var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                var temporary = path + ".tmp";
                File.WriteAllBytes(temporary, encrypted); File.Move(temporary, path, false);
                root = X509CertificateLoader.LoadPkcs12(bytes, null, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
    }

    public X509Certificate2 Get(params IPAddress[] addresses)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=SideWidget Remote", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        var san = new SubjectAlternativeNameBuilder();
        foreach (var address in addresses.Distinct()) san.AddIpAddress(address);
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using var signed = request.Create(root, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(90), RandomNumberGenerator.GetBytes(16));
        using var withKey = signed.CopyWithPrivateKey(key);
        // Schannel's TLS server needs an imported user key container on Windows.
        // No PersistKeySet: disposing the certificate also releases its temporary key container.
        var bytes = withKey.Export(X509ContentType.Pfx);
        try { return X509CertificateLoader.LoadPkcs12(bytes, null, X509KeyStorageFlags.UserKeySet); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public byte[] ExportRoot() => root.Export(X509ContentType.Cert);
    public static bool NeedsRenewal(X509Certificate2? certificate, DateTimeOffset now) => certificate is null || certificate.NotAfter.ToUniversalTime() <= now.UtcDateTime.AddDays(7);
    public string Fingerprint => Convert.ToHexString(SHA256.HashData(ExportRoot()));
    public void Dispose() => root.Dispose();
}
