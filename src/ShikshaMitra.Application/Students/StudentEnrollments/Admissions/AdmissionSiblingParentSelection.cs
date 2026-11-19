using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Server-owned provenance for the explicit parent-only admission flow.</summary>
public static class AdmissionSiblingParentSelection
{
	private const string FlowKey = "ParentAccountFlowVersion";
	private const string ConfirmationKey = "SiblingParentConfirmation";
	private const string RelationshipKey = "ParentAccountRelationship";

	public static bool IsParentFlow(AdmissionApplicant applicant) => applicant.GetProperty<int>(FlowKey) == 1;
	public static bool HasParentFlowProvenance(AdmissionApplicant applicant) =>
		applicant.ExtraProperties.ContainsKey(FlowKey) || applicant.ExtraProperties.ContainsKey(RelationshipKey) ||
		applicant.ExtraProperties.ContainsKey(ConfirmationKey);
	public static string? ReadRelationship(AdmissionApplicant applicant) => applicant.GetProperty<string>(RelationshipKey);

	public static AdmissionSiblingParentConfirmation? ReadConfirmation(AdmissionApplicant applicant)
	{
		var json = applicant.GetProperty<string>(ConfirmationKey);
		if (string.IsNullOrWhiteSpace(json)) return null;
		try
		{
			var confirmation = JsonSerializer.Deserialize<AdmissionSiblingParentConfirmation>(json);
			return confirmation is { ConfirmedBy: not null } && confirmation.FamilyId == applicant.FamilyId &&
				confirmation.SourceApplicantId != Guid.Empty && IsRelationship(confirmation.Relationship)
				? confirmation : null;
		}
		catch (JsonException) { return null; }
	}

	public static bool IsRelationship(string? relationship) => relationship is "father" or "mother";

	public static void CaptureParent(AdmissionApplicant applicant, string relationship, string name, string mobile, string? email)
	{
		if (!IsRelationship(relationship)) throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Choose Father or Mother.");
		var prefix = relationship == "father" ? "Father" : "Mother";
		applicant.SetProperty(FlowKey, 1);
		applicant.SetProperty(RelationshipKey, relationship);
		applicant.SetProperty("AdmissionStudentField." + prefix + ".Name", name.Trim());
		applicant.SetProperty("AdmissionStudentField." + prefix + ".Mobile", mobile.Trim());
		applicant.SetProperty("AdmissionStudentField." + prefix + ".Email", NormalizeEmail(email));
	}

	public static void Confirm(AdmissionApplicant applicant, AdmissionSiblingParentCandidateDto parent,
		Guid actorId, DateTime confirmedAt, string submittedMobile, string? submittedEmail)
	{
		CaptureParent(applicant, parent.Relationship!, parent.Name, parent.Mobile ?? string.Empty, parent.Email);
		applicant.SetProperty(ConfirmationKey, JsonSerializer.Serialize(new AdmissionSiblingParentConfirmation(
			applicant.FamilyId, parent.SourceApplicantId, parent.Relationship!, NormalizeEmail(parent.Email),
			actorId, confirmedAt, submittedMobile, NormalizeEmail(submittedEmail), parent.Name, parent.FirstName, parent.LastName, parent.Mobile)));
	}

	public static string? NormalizeEmail(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}

public sealed record AdmissionSiblingParentConfirmation(Guid FamilyId, Guid SourceApplicantId, string Relationship,
	string? Email, Guid? ConfirmedBy, DateTime ConfirmedAt, string SubmittedMobile, string? SubmittedEmail,
	string? Name = null, string? FirstName = null, string? LastName = null, string? Mobile = null);
