using System;
using System.Net.Mail;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Stores optional people on this applicant without rewriting shared family identity.</summary>
internal static class AdmissionFamilyContactCapture
{
	private const string InquiryContactKey = "MainSchoolContactSnapshot";
	private const string RequestKey = "AdmissionFamilyContactsRequest";

	public static bool MatchesRequest(AdmissionApplicant applicant, AdmissionFamilyContactsDto? input, string? parentRelationship) =>
		applicant.GetProperty<string>(RequestKey) == RequestSnapshot(input, parentRelationship);

	private static string? RequestSnapshot(AdmissionFamilyContactsDto? input, string? parentRelationship)
	{
		if (input == null) return null;
		return JsonSerializer.Serialize(new
		{
			MainContactRelationship = input.MainContactRelationship ?? parentRelationship,
			OtherParent = Person(input.OtherParent), OtherGuardian = Person(input.OtherGuardian)
		});
		static object? Person(AdmissionFamilyContactDto? person) => person == null ? null : new
		{
			FullName = person.FullName?.Trim(), Relationship = person.Relationship?.Trim(),
			Mobile = string.IsNullOrWhiteSpace(person.Mobile) ? null : string.Concat(person.Mobile.Where(char.IsDigit)),
			Email = AdmissionSiblingParentSelection.NormalizeEmail(person.Email)
		};
	}

	public static AdmissionSchoolContactDto? ReadMainContact(AdmissionApplicant applicant)
	{
		var role = applicant.GetProperty<string>(StudentFamilyContactSelection.SelectionKey) ?? AdmissionSiblingParentSelection.ReadRelationship(applicant);
		if (role == null) return null;
		var prefix = role switch { "father" => "Father", "mother" => "Mother", "guardian" => "Guardian", _ => throw Invalid("Review the saved main school contact.") };
		return new()
		{
			Relationship = role,
			RelationshipLabel = role == "guardian" ? applicant.GetProperty<string>(StudentFamilyContactSelection.GuardianRelationshipKey) : null,
			FullName = AdmissionStudentProfileMapping.Read(applicant, prefix + ".Name") ?? string.Empty,
			Mobile = AdmissionStudentProfileMapping.Read(applicant, prefix + ".Mobile"),
			Email = AdmissionStudentProfileMapping.Read(applicant, prefix + ".Email")
		};
	}

	public static void CopyToInquiry(AdmissionApplicant applicant, AdmissionInquiry inquiry)
	{
		var contact = ReadMainContact(applicant);
		if (contact != null) inquiry.SetProperty(InquiryContactKey, JsonSerializer.Serialize(contact));
	}

	public static AdmissionSchoolContactDto? ReadMainContact(AdmissionInquiry inquiry)
	{
		var json = inquiry.GetProperty<string>(InquiryContactKey);
		if (string.IsNullOrWhiteSpace(json)) return null;
		try { return JsonSerializer.Deserialize<AdmissionSchoolContactDto>(json) ?? throw Invalid("Review the saved main school contact."); }
		catch (JsonException) { throw Invalid("Review the saved main school contact."); }
	}
	public static void Capture(AdmissionApplicant applicant, AdmissionFamilyContactsDto? input, string? parentRelationship)
	{
		if (input == null) return;
		var otherParent = input.OtherParent;
		var guardian = input.OtherGuardian;
		if (otherParent != null && (!AdmissionSiblingParentSelection.IsRelationship(otherParent.Relationship) ||
			otherParent.Relationship == parentRelationship))
			throw Invalid("The other parent must be different from the selected Father or Mother.");
		Validate(otherParent);
		Validate(guardian);
		var main = input.MainContactRelationship ?? parentRelationship;
		if (main is not ("father" or "mother" or "guardian") || !Available(main))
			throw Invalid("Choose a saved parent or an added guardian as the main school contact.");
		if (main == "guardian" && guardian != null && string.IsNullOrWhiteSpace(guardian.Mobile) && string.IsNullOrWhiteSpace(guardian.Email))
			throw Invalid("Enter a mobile number or email for the main school contact.");
		if (otherParent != null) Save(otherParent.Relationship == "father" ? "Father" : "Mother", otherParent);
		if (guardian != null)
		{
			Save("Guardian", guardian);
			applicant.SetProperty(StudentFamilyContactSelection.GuardianRelationshipKey, guardian.Relationship.Trim());
		}
		applicant.SetProperty(StudentFamilyContactSelection.SelectionKey, main);
		applicant.SetProperty(RequestKey, RequestSnapshot(input, parentRelationship));

		bool Available(string role) => role == "guardian" ? guardian != null || HasCaptured("Guardian")
			: role == parentRelationship || otherParent?.Relationship == role || HasCaptured(role == "father" ? "Father" : "Mother");
		bool HasCaptured(string prefix) => !string.IsNullOrWhiteSpace(AdmissionStudentProfileMapping.Read(applicant, prefix + ".Name")) ||
			!string.IsNullOrWhiteSpace(AdmissionStudentProfileMapping.Read(applicant, prefix + ".Email")) ||
			!string.IsNullOrWhiteSpace(AdmissionStudentProfileMapping.Read(applicant, prefix + ".Mobile"));
		void Save(string prefix, AdmissionFamilyContactDto person)
		{
			applicant.SetProperty("AdmissionStudentField." + prefix + ".Name", person.FullName.Trim());
			applicant.SetProperty("AdmissionStudentField." + prefix + ".Email", AdmissionSiblingParentSelection.NormalizeEmail(person.Email));
			applicant.SetProperty("AdmissionStudentField." + prefix + ".Mobile", string.IsNullOrWhiteSpace(person.Mobile) ? null : AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile(person.Mobile));
		}
	}

	public static void ApplySelection(AdmissionApplicant applicant, Student student)
	{
		var selection = applicant.GetProperty<string>(StudentFamilyContactSelection.SelectionKey);
		// Older intake stored its primary adult as Guardian while defaulting the role to Father/Mother.
		// Preserve that same adult only for a new, unambiguous legacy identity, never an explicit choice.
		if (selection is "father" or "mother" && student.Id == applicant.ReservedStudentId && !applicant.ExistingStudentId.HasValue &&
			!AdmissionSiblingParentSelection.HasParentFlowProvenance(applicant) &&
			string.IsNullOrWhiteSpace(applicant.GetProperty<string>(RequestKey)) &&
			string.IsNullOrWhiteSpace(applicant.GetProperty<string>(StudentFamilyContactSelection.GuardianRelationshipKey)) &&
			!StudentFamilyContactSelection.Present(student.FamilyDetail?.FatherDetails) &&
			!StudentFamilyContactSelection.Present(student.FamilyDetail?.MotherDetails) &&
			StudentFamilyContactSelection.Present(student.FamilyDetail?.GuardianDetails))
			selection = "guardian";
		if (selection != null) StudentFamilyContactSelection.Select(student, selection);
		var relationship = applicant.GetProperty<string>(StudentFamilyContactSelection.GuardianRelationshipKey);
		if (relationship != null) student.SetProperty(StudentFamilyContactSelection.GuardianRelationshipKey, relationship);
	}

	private static void Validate(AdmissionFamilyContactDto? person)
	{
		if (person == null) return;
		if (string.IsNullOrWhiteSpace(person.FullName) || person.FullName.Trim().Length > 256 ||
			string.IsNullOrWhiteSpace(person.Relationship) || person.Relationship.Trim().Length > 128)
			throw Invalid("Enter the contact name and relationship.");
		if (!string.IsNullOrWhiteSpace(person.Mobile)) AdmissionInquiryPhoneValidator.NormalizeAndValidateIndianMobile(person.Mobile);
		if (!string.IsNullOrWhiteSpace(person.Email) && (person.Email.Length > 256 || person.Email.Contains('\r') || person.Email.Contains('\n') ||
			!MailAddress.TryCreate(person.Email.Trim(), out var email) || !string.Equals(email.Address, person.Email.Trim(), StringComparison.OrdinalIgnoreCase)))
			throw Invalid("Enter a valid contact email.");
	}

	private static BusinessException Invalid(string message) => new(AdmissionInquiryManagementErrorCodes.InvalidInput, message);
}
