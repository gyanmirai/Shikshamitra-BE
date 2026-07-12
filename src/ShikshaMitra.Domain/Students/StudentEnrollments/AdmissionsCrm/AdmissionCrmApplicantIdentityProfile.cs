using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmApplicantIdentityProfile : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid ApplicantPersonId { get; private set; }
	public Guid HouseholdId { get; private set; }
	public AdmissionCrmIdentityResolutionStatus Status { get; private set; }
	public bool AccountableContactReady { get; private set; }
	public bool IdentityConsentReady { get; private set; }
	public bool AccountLinkReady { get; private set; }
	public string? ConflictReason { get; private set; }
	public bool OverrideUsed { get; private set; }
	public string? OverrideReason { get; private set; }
	public Guid? ResolvedByUserId { get; private set; }
	public DateTime? ResolvedAt { get; private set; }

	protected AdmissionCrmApplicantIdentityProfile()
	{
	}

	public AdmissionCrmApplicantIdentityProfile(
		Guid id, Guid? tenantId, Guid academicYearId, Guid leadId,
		Guid applicantPersonId, Guid householdId) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		ApplicantPersonId = applicantPersonId;
		HouseholdId = householdId;
		Status = AdmissionCrmIdentityResolutionStatus.Unresolved;
	}

	public void Evaluate(bool accountableContactReady, bool identityConsentReady, bool accountLinkReady, string? conflictReason)
	{
		AccountableContactReady = accountableContactReady;
		IdentityConsentReady = identityConsentReady;
		AccountLinkReady = accountLinkReady;
		ConflictReason = Normalize(conflictReason);
		Status = OverrideUsed
			? AdmissionCrmIdentityResolutionStatus.Overridden
			: ConflictReason != null
			? AdmissionCrmIdentityResolutionStatus.Conflict
			: AccountableContactReady && IdentityConsentReady && AccountLinkReady
				? AdmissionCrmIdentityResolutionStatus.Ready
				: AdmissionCrmIdentityResolutionStatus.PendingReview;
	}

	public void Override(string reason, Guid userId, DateTime at)
	{
		OverrideReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		OverrideUsed = true;
		ResolvedByUserId = userId;
		ResolvedAt = at.ToUniversalTime();
		Status = AdmissionCrmIdentityResolutionStatus.Overridden;
	}

	private static string? Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : Check.Length(value.Trim(), nameof(value), AdmissionCrmConsts.MaxNotesLength);
}
