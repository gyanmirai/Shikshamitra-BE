using ShikshaMitra.Security;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace ShikshaMitra.Security;

public sealed class OpenIddictCertificateStore_Tests : IDisposable
{
	private readonly string _temporaryDirectory =
		Path.Combine(Path.GetTempPath(), $"shikshamitra-certificates-{Guid.NewGuid():N}");

	[Fact]
	public void GetOrCreate_ShouldCreateAndReuseAValidCertificate()
	{
		var path = CertificatePath();

		using var created = OpenIddictCertificateStore.GetOrCreate(
			path,
			"initial-password",
			"CN=ShikshaMitra Test Signing",
			X509KeyUsageFlags.DigitalSignature);
		using var reused = OpenIddictCertificateStore.GetOrCreate(
			path,
			"initial-password",
			"CN=ShikshaMitra Test Signing",
			X509KeyUsageFlags.DigitalSignature);

		File.Exists(path).ShouldBeTrue();
		reused.Thumbprint.ShouldBe(created.Thumbprint);
		reused.Extensions
			.OfType<X509KeyUsageExtension>()
			.Single()
			.KeyUsages.ShouldBe(X509KeyUsageFlags.DigitalSignature);
	}

	[Fact]
	public void GetOrCreate_ShouldReplaceCertificateWhenPasswordChanges()
	{
		var path = CertificatePath();

		using var original = OpenIddictCertificateStore.GetOrCreate(
			path,
			"old-password",
			"CN=ShikshaMitra Test Encryption",
			X509KeyUsageFlags.KeyEncipherment);
		using var replacement = OpenIddictCertificateStore.GetOrCreate(
			path,
			"new-password",
			"CN=ShikshaMitra Test Encryption",
			X509KeyUsageFlags.KeyEncipherment);

		replacement.Thumbprint.ShouldNotBe(original.Thumbprint);
		Should.NotThrow(() =>
		{
			using var persisted = X509CertificateLoader.LoadPkcs12FromFile(
				path,
				"new-password",
				X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
		});
	}

	[Fact]
	public void GetOrCreate_ShouldReplaceACorruptCertificateFile()
	{
		var path = CertificatePath();
		Directory.CreateDirectory(_temporaryDirectory);
		File.WriteAllText(path, "not a certificate");

		using var certificate = OpenIddictCertificateStore.GetOrCreate(
			path,
			"replacement-password",
			"CN=ShikshaMitra Test Signing",
			X509KeyUsageFlags.DigitalSignature);

		certificate.HasPrivateKey.ShouldBeTrue();
		Should.NotThrow(() =>
		{
			using var persisted = X509CertificateLoader.LoadPkcs12FromFile(
				path,
				"replacement-password",
				X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
		});
	}

	[Fact]
	public void GetOrCreate_ShouldRotateAnExpiredCertificateFile()
	{
		var path = CertificatePath();
		using var original = OpenIddictCertificateStore.GetOrCreate(
			path,
			"rotation-password",
			"CN=ShikshaMitra Test Signing",
			X509KeyUsageFlags.DigitalSignature);
		File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-181));

		using var replacement = OpenIddictCertificateStore.GetOrCreate(
			path,
			"rotation-password",
			"CN=ShikshaMitra Test Signing",
			X509KeyUsageFlags.DigitalSignature);

		replacement.Thumbprint.ShouldNotBe(original.Thumbprint);
	}

	public void Dispose()
	{
		if (Directory.Exists(_temporaryDirectory))
		{
			Directory.Delete(_temporaryDirectory, true);
		}
	}

	private string CertificatePath()
	{
		return Path.Combine(_temporaryDirectory, "certificate.pfx");
	}
}
