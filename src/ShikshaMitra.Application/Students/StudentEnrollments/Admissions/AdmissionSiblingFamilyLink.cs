using System;
using System.Collections.Generic;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Sibling membership and this child's contact snapshot, independent of portal-account proof.</summary>
internal static class AdmissionSiblingFamilyLink
{
	internal const string ProofKey = "AdmissionSiblingFamilyLink";
	internal const string ContactKey = "AdmissionSiblingOwnContact";

	public static bool HasOwnContact(AdmissionApplicant applicant) => applicant.ExtraProperties.ContainsKey(ContactKey);

	public static void Capture(AdmissionApplicant applicant, Guid sourceApplicantId, Guid actorId, DateTime now)
	{
		if (sourceApplicantId == Guid.Empty || sourceApplicantId == applicant.Id || actorId == Guid.Empty)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput);
		applicant.SetProperty(ProofKey, JsonSerializer.Serialize(new FamilyLink(1, applicant.FamilyId, sourceApplicantId, actorId, now)));
	}

	public static void CaptureContact(AdmissionApplicant applicant, string firstName, string? lastName, string mobile, string? email) =>
		applicant.SetProperty(ContactKey, JsonSerializer.Serialize(new OwnContact(firstName, lastName, mobile, email)));

	public static void CaptureContact(AdmissionApplicant applicant, AdmissionGuardian guardian) =>
		CaptureContact(applicant, guardian.FirstName, guardian.LastName, guardian.Mobile, guardian.Email);

	/// <summary>A detached contact view; it must never be persisted as a shared family guardian.</summary>
	public static AdmissionGuardian? OwnGuardian(AdmissionApplicant applicant)
	{
		if (!HasOwnContact(applicant)) return null;
		OwnContact? contact;
		try { contact = JsonSerializer.Deserialize<OwnContact>(applicant.GetProperty<string>(ContactKey) ?? "null"); }
		catch (JsonException) { throw InvalidContact(); }
		if (contact == null || string.IsNullOrWhiteSpace(contact.FirstName) || string.IsNullOrWhiteSpace(contact.Mobile)) throw InvalidContact();
		return new AdmissionGuardian(applicant.Id, applicant.TenantId, applicant.FamilyId, contact.FirstName, contact.LastName,
			contact.Mobile, contact.Email, AdmissionGuardianRelationship.Parent, true);
	}

	public static bool ApplyContactValue(AdmissionApplicant applicant, string key, string value)
	{
		if (!HasOwnContact(applicant) || key is not ("Guardian.FirstName" or "Guardian.LastName" or "Guardian.Mobile" or "Guardian.Email")) return false;
		var contact = OwnGuardian(applicant)!;
		contact.UpdateContact(key == "Guardian.FirstName" ? value : contact.FirstName,
			key == "Guardian.LastName" ? value : contact.LastName,
			key == "Guardian.Mobile" ? value : contact.Mobile,
			key == "Guardian.Email" ? value : contact.Email);
		CaptureContact(applicant, contact);
		return true;
	}

	public static bool ApplyParentContactValue(AdmissionApplicant applicant, string key, string value)
	{
		if (!IsSelectedParentContact(applicant, key)) return false;
		// An optional structured clear does not erase the independently required generic contact.
		if (IsOptionalIdentityClear(key, value)) return AdmissionAdditionalStudentFieldCatalog.ApplyValue(key, value, applicant);
		var contact = OwnGuardian(applicant)!;
		ChangeContact(contact, key, value);
		if (!AdmissionAdditionalStudentFieldCatalog.ApplyValue(key, value, applicant)) return false;
		CaptureContact(applicant, contact);
		return true;
	}

	/// <summary>Explicit structured parent answers keep their existing precedence over generic aliases.</summary>
	public static void SynchronizeStructuredContact(AdmissionApplicant applicant, IReadOnlyDictionary<string, string> answers)
	{
		foreach (var key in answers.Keys)
		{
			if (!IsSelectedParentContact(applicant, key)) continue;
			var value = AdmissionStudentProfileMapping.Read(applicant, key) ?? string.Empty;
			if (IsOptionalIdentityClear(key, value)) continue;
			var contact = OwnGuardian(applicant)!;
			ChangeContact(contact, key, value);
			CaptureContact(applicant, contact);
		}
	}

	private static bool IsOptionalIdentityClear(string key, string value) => string.IsNullOrWhiteSpace(value) &&
		(key.EndsWith(".Name", StringComparison.Ordinal) || key.EndsWith(".Mobile", StringComparison.Ordinal));

	private static bool IsSelectedParentContact(AdmissionApplicant applicant, string key)
	{
		if (!HasOwnContact(applicant)) return false;
		var role = AdmissionPrimaryContactSelection.ReadRelationship(applicant);
		var prefix = role == "father" ? "Father" : role == "mother" ? "Mother" : null;
		return prefix != null && (key == prefix + ".Name" || key == prefix + ".Mobile" || key == prefix + ".Email");
	}

	private static void ChangeContact(AdmissionGuardian contact, string key, string value) =>
		contact.UpdateContact(key.EndsWith(".Name", StringComparison.Ordinal) ? value : contact.FirstName,
			key.EndsWith(".Name", StringComparison.Ordinal) ? null : contact.LastName,
			key.EndsWith(".Mobile", StringComparison.Ordinal) ? value : contact.Mobile,
			key.EndsWith(".Email", StringComparison.Ordinal) ? value : contact.Email);

	private static BusinessException InvalidContact() => new(AdmissionInquiryManagementErrorCodes.InvalidState, "Review this child's saved primary contact.");
	private sealed record FamilyLink(int Version, Guid FamilyId, Guid SourceApplicantId, Guid ConfirmedBy, DateTime ConfirmedAt);
	private sealed record OwnContact(string FirstName, string? LastName, string Mobile, string? Email);
}
