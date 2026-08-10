using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public static class AdmissionCrmIdentityMapper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionCrmPersonDto Map(AdmissionCrmPerson person) => new()
	{
		Id = person.Id,
		StudentId = person.StudentId,
		UserId = person.UserId,
		FirstName = person.FirstName,
		MiddleName = person.MiddleName,
		LastName = person.LastName,
		DateOfBirth = person.DateOfBirth,
		Phone = person.Phone,
		Email = person.Email,
		Provenance = person.Provenance,
		CoreIdentityVerified = person.CoreIdentityVerified,
		CoreIdentityVerifiedAt = person.CoreIdentityVerifiedAt,
		AadhaarMasked = string.IsNullOrWhiteSpace(person.AadhaarLast4) ? null : $"XXXX-XXXX-{person.AadhaarLast4}",
		AadhaarStatus = person.AadhaarStatus,
		PenNumber = person.PenNumber,
		PenStatus = person.PenStatus,
		ApaarId = person.ApaarId,
		ApaarConsentStatus = person.ApaarConsentStatus,
		ApaarStatus = person.ApaarStatus,
		UdiseStudentCode = person.UdiseStudentCode,
		UdiseStatus = person.UdiseStatus
	};

	public static AdmissionCrmIdentityConsentDto Map(AdmissionCrmIdentityConsent entity) => new()
	{
		Id = entity.Id,
		PersonId = entity.PersonId,
		Purpose = entity.Purpose,
		TextVersion = entity.TextVersion,
		Language = entity.Language,
		Actor = entity.Actor,
		Channel = entity.Channel,
		IsGranted = entity.IsGranted,
		RecordedAt = entity.RecordedAt,
		WithdrawnAt = entity.WithdrawnAt,
		WithdrawalReason = entity.WithdrawalReason
	};

	public static AdmissionCrmPersonInputDto ResolveApplicant(
		AdmissionCrmPersonInputDto input,
		AdmissionInquiryLead lead) => new()
	{
		FirstName = string.IsNullOrWhiteSpace(input.FirstName) ? lead.FirstName ?? "Applicant" : input.FirstName,
		MiddleName = input.MiddleName ?? lead.MiddleName,
		LastName = input.LastName ?? lead.LastName,
		DateOfBirth = input.DateOfBirth ?? lead.DateOfBirth?.UtcDateTime,
		Phone = input.Phone ?? lead.PrimaryContactNo,
		Email = input.Email ?? lead.Email,
		Provenance = input.Provenance
	};

	public static string FullName(AdmissionCrmPerson person) => string.Join(
		" ",
		new[] { person.FirstName, person.MiddleName, person.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));

	public static string? NormalizeContact(string? value) => string.IsNullOrWhiteSpace(value)
		? null
		: new string(value.Where(char.IsDigit).ToArray());

	public static string Sha256(string value) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

	public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
	public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions);
}
