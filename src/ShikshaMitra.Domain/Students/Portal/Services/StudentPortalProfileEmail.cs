using System;
using System.Text.Json;
using ShikshaMitra.Students.ValueObjects;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.Portal;

/// <summary>Email-only mutation; all other contact fields and family relationships are retained.</summary>
public static class StudentPortalProfileEmail
{
	private const string StudentProofKey = "OnboardingStudentEmailCapture";
	public static string? Read(Student student, string relationship) => relationship == "self"
		? student.ContactDetail?.Email : Parent(student, relationship)?.Email;
	public static ParentGuardianDetails? Parent(Student student, string relationship) => relationship switch
	{
		"father" => student.FamilyDetail?.FatherDetails,
		"mother" => student.FamilyDetail?.MotherDetails,
		"guardian" => student.FamilyDetail?.GuardianDetails,
		_ => null
	};
	public static void Apply(Student student, string relationship, string email, Guid actorId)
	{
		if (relationship == "self")
		{
			var contact = student.ContactDetail;
			student.ApplyVerifiedRegistrationContact(contact?.PrimaryContactNo ?? string.Empty, contact?.SecondaryContactNo,
				email, contact?.Address1, contact?.Address2, contact?.Address3, contact?.City, contact?.State, contact?.Country, contact?.Pincode);
			student.SetProperty(StudentProofKey, JsonSerializer.Serialize(new Capture(student.Id, student.TenantId, email, actorId)));
			return;
		}
		var parent = Parent(student, relationship) ?? throw new ArgumentException("A saved Father or Mother is required.", nameof(relationship));
		var updated = new ParentGuardianDetails(parent.FullName, parent.DateOfBirth, parent.Occupation, email,
			parent.ContactNo, parent.OfficeNo, parent.Qualification, parent.YearOfPassing, parent.Designation,
			parent.Organization, parent.AadharNo, parent.OfficeAddress, parent.LocalAddress);
		if (relationship == "father") student.FamilyDetail!.UpdateFatherDetails(updated);
		else if (relationship == "mother") student.FamilyDetail!.UpdateMotherDetails(updated);
		else student.FamilyDetail!.UpdateGuardianDetails(updated);
	}
	public static bool HasStudentCapture(Student student, string email)
	{
		try
		{
			var capture = JsonSerializer.Deserialize<Capture>(student.GetProperty<string>(StudentProofKey) ?? "null");
			return capture != null && capture.StudentId == student.Id && capture.TenantId == student.TenantId &&
				capture.ActorId != Guid.Empty && Same(capture.Email, email);
		}
		catch (JsonException) { return false; }
	}
	public static bool Same(string? first, string? second) => string.Equals(first?.Trim() ?? string.Empty, second?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
	private sealed record Capture(Guid StudentId, Guid? TenantId, string Email, Guid ActorId);
}
