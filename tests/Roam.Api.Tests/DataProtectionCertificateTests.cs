using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Roam.Api.Security;
using Xunit;

namespace Roam.Api.Tests;

public class DataProtectionCertificateTests
{
    [Fact]
    public void Load_ReturnsNull_WhenNotConfigured()
    {
        Assert.Null(DataProtectionCertificate.Load(null, null));
        Assert.Null(DataProtectionCertificate.Load("  ", "pw"));
    }

    [Fact]
    public void Load_ReadsPasswordProtectedPfx()
    {
        var (base64, password) = CreatePfx();

        using var certificate = DataProtectionCertificate.Load(base64, password);

        Assert.NotNull(certificate);
        Assert.True(certificate!.HasPrivateKey);
    }

    [Fact]
    public void Load_RejectsWrongPasswordAndGarbage_WithClearErrors()
    {
        var (base64, _) = CreatePfx();

        var wrongPassword = Assert.Throws<InvalidOperationException>(() => DataProtectionCertificate.Load(base64, "wrong"));
        Assert.Contains("DATA_PROTECTION_CERT_PASSWORD", wrongPassword.Message);

        var garbage = Assert.Throws<InvalidOperationException>(() => DataProtectionCertificate.Load("not base64!", "x"));
        Assert.Contains("base64", garbage.Message);
    }

    [Fact]
    public void KeysProtectedWithTheCertificate_AreEncryptedAtRest_AndStillUsable()
    {
        var (base64, password) = CreatePfx();
        var certificate = DataProtectionCertificate.Load(base64, password)!;
        var repository = new InMemoryXmlRepository();

        ServiceProvider Build() => new ServiceCollection()
            .AddDataProtection().SetApplicationName("Roam")
            .ProtectKeysWithCertificate(certificate).UnprotectKeysWithAnyCertificate(certificate)
            .Services
            .Configure<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(o => o.XmlRepository = repository)
            .BuildServiceProvider();

        string token;
        using (var writer = Build())
        {
            token = writer.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("hello");
        }

        // What is stored contains no raw key material.
        var stored = Assert.Single(repository.GetAllElements()).ToString();
        Assert.Contains("EncryptedData", stored);
        Assert.DoesNotContain("<masterKey", stored);

        // A second instance (another server) with the same certificate can read tokens from the first.
        using var reader = Build();
        Assert.Equal("hello", reader.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Unprotect(token));
    }

    private static (string Base64, string Password) CreatePfx()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=roam-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        const string password = "test-password";
        return (Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, password)), password);
    }

    private sealed class InMemoryXmlRepository : IXmlRepository
    {
        private readonly List<XElement> _elements = new();
        public IReadOnlyCollection<XElement> GetAllElements() => _elements.Select(e => new XElement(e)).ToList();
        public void StoreElement(XElement element, string friendlyName) => _elements.Add(new XElement(element));
    }
}
