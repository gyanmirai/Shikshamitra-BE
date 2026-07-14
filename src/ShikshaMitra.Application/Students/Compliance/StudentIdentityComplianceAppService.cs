using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Security.Encryption;

namespace ShikshaMitra.Students.Compliance;

[Authorize]
[RemoteService(false)]
public class StudentIdentityComplianceAppService(
	IRepository<StudentIdentityCompliance, Guid> complianceRepository,
	StudentScopeAccessService studentScopeAccessService,
	IStringEncryptionService stringEncryptionService,
	IGuidGenerator guidGenerator) : ApplicationService, IStudentIdentityComplianceAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.View)]
	public async Task<StudentIdentityComplianceDto> GetAsync(Guid studentId)
	{
		await studentScopeAccessService.GetStudentAsync(studentId, nameof(GetAsync));
		var compliance = await complianceRepository.FirstOrDefaultAsync(x => x.StudentId == studentId);
		return compliance == null
			? EmptyDto(studentId)
			: MapToDto(compliance);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.ManageSensitive)]
	public async Task<StudentIdentityComplianceDto> UpdateAsync(Guid studentId, UpdateStudentIdentityComplianceDto input)
	{
		await studentScopeAccessService.GetStudentAsync(studentId, nameof(UpdateAsync));
		return await UpsertFromRegistrationAsync(studentId, input);
	}

	public async Task<StudentIdentityComplianceDto> UpsertFromRegistrationAsync(Guid studentId, UpdateStudentIdentityComplianceDto input)
	{
		var compliance = await GetOrCreateAsync(studentId);

		var aadhaarInput = input.AadhaarNo?.Trim();
		var shouldPreserveExistingAadhaar =
			string.IsNullOrWhiteSpace(aadhaarInput) && !string.IsNullOrWhiteSpace(compliance.AadhaarEncrypted) ||
			IsMaskedAadhaar(aadhaarInput);

		var aadhaar = shouldPreserveExistingAadhaar
			? null
			: NormalizeDigits(input.AadhaarNo);

		if (!shouldPreserveExistingAadhaar && !string.IsNullOrWhiteSpace(aadhaar) && aadhaar.Length != 12)
		{
			throw new BusinessException("StudentCompliance:AadhaarInvalid")
				.WithData("Reason", "Aadhaar must contain exactly 12 digits.");
		}

		if (!shouldPreserveExistingAadhaar)
		{
			compliance.SetAadhaar(
				string.IsNullOrWhiteSpace(aadhaar) ? null : stringEncryptionService.Encrypt(aadhaar),
				string.IsNullOrWhiteSpace(aadhaar) ? null : Sha256(aadhaar),
				string.IsNullOrWhiteSpace(aadhaar) ? null : aadhaar[^4..],
				input.AadhaarName,
				input.AadhaarVerificationStatus);
		}

		compliance.SetApaar(
			NormalizeIdentifier(input.ApaarId),
			input.ApaarConsentStatus,
			input.ApaarConsentDate,
			input.ApaarVerificationStatus);

		compliance.SetAdministrativeIdentifiers(
			NormalizeIdentifier(input.PenNumber),
			NormalizeIdentifier(input.UdiseStudentCode),
			input.UdiseSyncStatus);

		compliance.SetBenefitTracking(
			input.RteStatus,
			input.ScholarshipStatus,
			input.ScholarshipSchemeName,
			input.ScholarshipApplicationNo,
			input.CasteCertificateNo,
			input.CasteCertificateVerifiedStatus);

		await complianceRepository.UpdateAsync(compliance, autoSave: true);
		return MapToDto(compliance);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.ManageSensitive)]
	public async Task<StudentIdentityComplianceDto> UpdateAadhaarVerificationStatusAsync(Guid studentId, UpdateAadhaarVerificationStatusDto input)
	{
		await studentScopeAccessService.GetStudentAsync(studentId, nameof(UpdateAadhaarVerificationStatusAsync));
		var compliance = await GetOrCreateAsync(studentId);
		compliance.SetAadhaarVerificationStatus(input.Status);
		await complianceRepository.UpdateAsync(compliance, autoSave: true);
		return MapToDto(compliance);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.ManageSensitive)]
	public async Task<StudentIdentityComplianceDto> UpdateApaarConsentAsync(Guid studentId, UpdateApaarConsentDto input)
	{
		await studentScopeAccessService.GetStudentAsync(studentId, nameof(UpdateApaarConsentAsync));
		var compliance = await GetOrCreateAsync(studentId);
		compliance.SetApaarConsent(input.ConsentStatus, input.ConsentDate);
		await complianceRepository.UpdateAsync(compliance, autoSave: true);
		return MapToDto(compliance);
	}

	private async Task<StudentIdentityCompliance> GetOrCreateAsync(Guid studentId)
	{
		var existing = await complianceRepository.FirstOrDefaultAsync(x => x.StudentId == studentId);
		if (existing != null)
		{
			return existing;
		}

		var compliance = new StudentIdentityCompliance(guidGenerator.Create(), CurrentTenant.Id, studentId);
		return await complianceRepository.InsertAsync(compliance, autoSave: true);
	}

	private static StudentIdentityComplianceDto MapToDto(StudentIdentityCompliance compliance) => new()
	{
		StudentId = compliance.StudentId,
		AadhaarMasked = MaskAadhaar(compliance.AadhaarLast4),
		AadhaarName = compliance.AadhaarName,
		AadhaarVerificationStatus = compliance.AadhaarVerificationStatus,
		ApaarId = compliance.ApaarId,
		ApaarConsentStatus = compliance.ApaarConsentStatus,
		ApaarConsentDate = compliance.ApaarConsentDate,
		ApaarVerificationStatus = compliance.ApaarVerificationStatus,
		PenNumber = compliance.PenNumber,
		UdiseStudentCode = compliance.UdiseStudentCode,
		UdiseSyncStatus = compliance.UdiseSyncStatus,
		UdiseLastExportedAt = compliance.UdiseLastExportedAt,
		RteStatus = compliance.RteStatus,
		ScholarshipStatus = compliance.ScholarshipStatus,
		ScholarshipSchemeName = compliance.ScholarshipSchemeName,
		ScholarshipApplicationNo = compliance.ScholarshipApplicationNo,
		CasteCertificateNo = compliance.CasteCertificateNo,
		CasteCertificateVerifiedStatus = compliance.CasteCertificateVerifiedStatus
	};

	private static StudentIdentityComplianceDto EmptyDto(Guid studentId) => new()
	{
		StudentId = studentId,
		AadhaarVerificationStatus = IdentityVerificationStatus.NotProvided,
		ApaarConsentStatus = ConsentStatus.NotAsked,
		ApaarVerificationStatus = IdentityVerificationStatus.NotProvided,
		UdiseSyncStatus = UdiseSyncStatus.NotReady,
		ScholarshipStatus = ScholarshipStatus.NotApplicable,
		CasteCertificateVerifiedStatus = IdentityVerificationStatus.NotProvided
	};

	private static string? MaskAadhaar(string? last4) =>
		string.IsNullOrWhiteSpace(last4) ? null : $"XXXX-XXXX-{last4}";

	private static string? NormalizeDigits(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		var digits = new string(value.Where(char.IsDigit).ToArray());
		return string.IsNullOrWhiteSpace(digits) ? null : digits;
	}

	private static string? NormalizeIdentifier(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

	private static bool IsMaskedAadhaar(string? value) =>
		!string.IsNullOrWhiteSpace(value) &&
		value.Trim().StartsWith("XXXX", StringComparison.OrdinalIgnoreCase) &&
		NormalizeDigits(value)?.Length == 4;

	private static string Sha256(string value)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		return Convert.ToHexString(hash);
	}
}
