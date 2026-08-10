using System;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal sealed class AdmissionCrmDuplicateProfileProbe
{
	private AdmissionCrmDuplicateProfileProbe(
		string? firstName,
		string? lastName,
		DateTimeOffset? dateOfBirth,
		string? parentGuardianName,
		Guid? targetStandardId,
		string? desiredClassName)
	{
		FirstName = firstName;
		LastName = lastName;
		DateOfBirth = dateOfBirth;
		ParentGuardianName = parentGuardianName;
		TargetStandardId = targetStandardId;
		DesiredClassName = desiredClassName;
	}

	public string? FirstName { get; }
	public string? LastName { get; }
	public DateTimeOffset? DateOfBirth { get; }
	public string? ParentGuardianName { get; }
	public Guid? TargetStandardId { get; }
	public string? DesiredClassName { get; }

	public static AdmissionCrmDuplicateProfileProbe FromLead(AdmissionInquiryLead lead) =>
		new(
			AdmissionCrmLeadDuplicateMatcher.NormalizeText(lead.FirstName),
			AdmissionCrmLeadDuplicateMatcher.NormalizeText(lead.LastName),
			lead.DateOfBirth,
			AdmissionCrmLeadDuplicateMatcher.NormalizeText(lead.ParentGuardianName),
			lead.TargetStandardId,
			AdmissionCrmLeadDuplicateMatcher.NormalizeText(lead.DesiredClassName));

	public static AdmissionCrmDuplicateProfileProbe FromInput(AdmissionCrmLeadDuplicatePreviewInput input) =>
		new(
			AdmissionCrmLeadDuplicateMatcher.NormalizeText(input.FirstName),
			AdmissionCrmLeadDuplicateMatcher.NormalizeText(input.LastName),
			input.DateOfBirth,
			AdmissionCrmLeadDuplicateMatcher.NormalizeText(input.ParentGuardianName),
			input.TargetStandardId,
			AdmissionCrmLeadDuplicateMatcher.NormalizeText(input.DesiredClassName));

	public static AdmissionCrmDuplicateProfileProbe FromCreateInput(CreateAdmissionCrmLeadDto input) =>
		new(
			AdmissionCrmLeadDuplicateMatcher.NormalizeText(input.FirstName),
			AdmissionCrmLeadDuplicateMatcher.NormalizeText(input.LastName),
			input.DateOfBirth,
			AdmissionCrmLeadDuplicateMatcher.NormalizeText(input.ParentGuardianName),
			input.TargetStandardId,
			AdmissionCrmLeadDuplicateMatcher.NormalizeText(input.DesiredClassName));

	public bool HasMinimumProfile() =>
		FirstName != null &&
		(ParentGuardianName != null || DateOfBirth.HasValue) &&
		(TargetStandardId.HasValue || DesiredClassName != null);
}