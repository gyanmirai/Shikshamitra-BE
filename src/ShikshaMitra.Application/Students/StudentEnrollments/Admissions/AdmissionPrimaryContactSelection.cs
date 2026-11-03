using System;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Primary school contact is independent of shared sibling-parent account authority.</summary>
internal static class AdmissionPrimaryContactSelection
{
	private const string RoleKey = "AdmissionPrimaryContactRelationship";
	private const string ActorKey = "AdmissionGuardianResponsibilityConfirmedBy";
	private const string TimeKey = "AdmissionGuardianResponsibilityConfirmedAt";

	public static bool IsRelationship(string? role) => role is "father" or "mother" or "guardian";
	public static bool HasSelection(AdmissionApplicant applicant) => applicant.ExtraProperties.ContainsKey(RoleKey);
	public static bool HasAdditionalMainGuardian(AdmissionApplicant applicant) => applicant.GetProperty<string>(RoleKey) == "guardian" &&
		!string.IsNullOrWhiteSpace(applicant.GetProperty<string>("AdmissionFamilyContactsRequest"));
	public static string? ReadRelationship(AdmissionApplicant applicant) => applicant.GetProperty<string>(RoleKey) ?? AdmissionSiblingParentSelection.ReadRelationship(applicant);
	public static bool GuardianResponsibilityConfirmed(AdmissionApplicant applicant) =>
		Guid.TryParse(applicant.GetProperty<string>(ActorKey), out var actor) && actor != Guid.Empty &&
		!string.IsNullOrWhiteSpace(applicant.GetProperty<string>(TimeKey));

	public static void EnsureReady(AdmissionApplicant applicant)
	{
		// Untouched historical records retain their original rules.
		if (applicant.GetProperty<string>(RoleKey) == "guardian" && !GuardianResponsibilityConfirmed(applicant))
			throw Invalid("School staff must confirm that both parents are not handling school-contact responsibility before qualifying this guardian contact.");
	}

	public static void CaptureMainGuardian(AdmissionApplicant applicant, AdmissionFamilyContactsDto? contacts,
		bool guardianResponsibilityConfirmed, Guid? actorId, DateTime now, bool isPublic)
	{
		if (contacts?.MainContactRelationship != "guardian" || applicant.ExistingStudentId.HasValue) return;
		var trusted = !isPublic && guardianResponsibilityConfirmed && actorId.HasValue && actorId.Value != Guid.Empty;
		if (!isPublic && !trusted)
			throw Invalid("Confirm that both parents are not handling school-contact responsibility before choosing Guardian.");
		// FamilyContactCapture owns the guardian profile and current school-contact choice.
		// A separately confirmed sibling parent remains the account owner.
		applicant.SetProperty(RoleKey, "guardian");
		if (trusted)
		{
			applicant.SetProperty(ActorKey, actorId!.Value.ToString("D"));
			applicant.SetProperty(TimeKey, now.ToUniversalTime().ToString("O"));
		}
	}

	public static void Capture(AdmissionApplicant applicant, string role, string name, string mobile, string? email,
		bool guardianResponsibilityConfirmed, Guid? actorId, DateTime now, bool isPublic)
	{
		if (!IsRelationship(role)) throw Invalid("Choose Father, Mother, or Guardian as the primary contact.");
		if (applicant.ExistingStudentId.HasValue || AdmissionSiblingParentSelection.ReadConfirmation(applicant) != null) return;
		var previousRole = applicant.GetProperty<string>(RoleKey);
		var trustedConfirmation = !isPublic && guardianResponsibilityConfirmed && actorId.HasValue && actorId.Value != Guid.Empty;
		if (role == "guardian" && !isPublic && !trustedConfirmation &&
			!(previousRole == "guardian" && GuardianResponsibilityConfirmed(applicant)))
			throw Invalid("Confirm that both parents are not handling school-contact responsibility before choosing Guardian.");
		if (role == "guardian")
		{
			applicant.ExtraProperties.Remove("ParentAccountFlowVersion");
			applicant.ExtraProperties.Remove("ParentAccountRelationship");
			applicant.SetProperty("AdmissionStudentField.Guardian.Name", name.Trim());
			applicant.SetProperty("AdmissionStudentField.Guardian.Mobile", mobile.Trim());
			applicant.SetProperty("AdmissionStudentField.Guardian.Email", AdmissionSiblingParentSelection.NormalizeEmail(email));
			if (trustedConfirmation)
			{
				applicant.SetProperty(ActorKey, actorId!.Value.ToString("D"));
				applicant.SetProperty(TimeKey, now.ToUniversalTime().ToString("O"));
			}
		}
		else
		{
			if (isPublic)
			{
				var prefix = role == "father" ? "Father" : "Mother";
				applicant.SetProperty("AdmissionStudentField." + prefix + ".Name", name.Trim());
				applicant.SetProperty("AdmissionStudentField." + prefix + ".Mobile", mobile.Trim());
				applicant.SetProperty("AdmissionStudentField." + prefix + ".Email", AdmissionSiblingParentSelection.NormalizeEmail(email));
			}
			else AdmissionSiblingParentSelection.CaptureParent(applicant, role, name, mobile, email);
			applicant.ExtraProperties.Remove(ActorKey);
			applicant.ExtraProperties.Remove(TimeKey);
		}
		applicant.SetProperty(RoleKey, role);
		applicant.SetProperty(StudentFamilyContactSelection.SelectionKey, role);
	}

	private static BusinessException Invalid(string message) => new(AdmissionInquiryManagementErrorCodes.InvalidInput, message);
}
