using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ShikshaMitra.Security;

public static class OpenIddictCertificateStore
{
	private static readonly TimeSpan MaximumCertificateFileAge = TimeSpan.FromDays(180);
	private const X509KeyStorageFlags KeyStorageFlags =
		X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet;

	public static X509Certificate2 GetOrCreate(
		string certificatePath,
		string? password,
		string subjectName,
		X509KeyUsageFlags keyUsage)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
		ArgumentException.ThrowIfNullOrWhiteSpace(subjectName);

		var directory = Path.GetDirectoryName(certificatePath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}

		if (File.Exists(certificatePath) && !IsExpired(certificatePath))
		{
			try
			{
				return LoadFromFile(certificatePath, password);
			}
			catch (CryptographicException)
			{
				File.Delete(certificatePath);
			}
		}
		else
		{
			File.Delete(certificatePath);
		}

		return CreateAndPersist(certificatePath, password, subjectName, keyUsage);
	}

	private static bool IsExpired(string certificatePath)
	{
		return DateTime.UtcNow - File.GetLastWriteTimeUtc(certificatePath) > MaximumCertificateFileAge;
	}

	private static X509Certificate2 CreateAndPersist(
		string certificatePath,
		string? password,
		string subjectName,
		X509KeyUsageFlags keyUsage)
	{
		using var algorithm = RSA.Create(2048);
		var request = new CertificateRequest(
			new X500DistinguishedName(subjectName),
			algorithm,
			HashAlgorithmName.SHA256,
			RSASignaturePadding.Pkcs1);
		request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, true));

		using var certificate = request.CreateSelfSigned(
			DateTimeOffset.UtcNow.AddMinutes(-5),
			DateTimeOffset.UtcNow.AddYears(2));
		var pfxData = certificate.Export(X509ContentType.Pfx, password);
		var temporaryPath = $"{certificatePath}.{Guid.NewGuid():N}.tmp";

		try
		{
			File.WriteAllBytes(temporaryPath, pfxData);

			try
			{
				File.Move(temporaryPath, certificatePath, false);
				return LoadFromBytes(pfxData, password);
			}
			catch (IOException) when (File.Exists(certificatePath))
			{
				return LoadFromFile(certificatePath, password);
			}
		}
		finally
		{
			File.Delete(temporaryPath);
		}
	}

	private static X509Certificate2 LoadFromFile(string certificatePath, string? password)
	{
		return X509CertificateLoader.LoadPkcs12FromFile(certificatePath, password, KeyStorageFlags);
	}

	private static X509Certificate2 LoadFromBytes(byte[] pfxData, string? password)
	{
		return X509CertificateLoader.LoadPkcs12(pfxData, password, KeyStorageFlags);
	}
}
