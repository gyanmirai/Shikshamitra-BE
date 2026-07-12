using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmApplicantChecklistProgress : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string ChecklistKey { get; private set; } = string.Empty;
	public AdmissionCrmApplicantChecklistProgressStatus Status { get; private set; }
	public string? Notes { get; private set; }
	public DateTimeOffset StatusChangedAt { get; private set; }

	protected AdmissionCrmApplicantChecklistProgress()
	{
	}

	public AdmissionCrmApplicantChecklistProgress(
		Guid id,
		Guid? tenantId,
		Guid leadId,
		Guid academicYearId,
		string checklistKey,
		AdmissionCrmApplicantChecklistProgressStatus status,
		string? notes,
		DateTimeOffset statusChangedAt)
		: base(id)
	{
		TenantId = tenantId;
		LeadId = leadId;
		AcademicYearId = academicYearId;
		ChecklistKey = Check.NotNullOrWhiteSpace(
			checklistKey,
			nameof(checklistKey),
			maxLength: AdmissionCrmConsts.MaxPurposeLength);
		UpdateStatus(status, notes, statusChangedAt);
	}

	public void UpdateStatus(
		AdmissionCrmApplicantChecklistProgressStatus status,
		string? notes,
		DateTimeOffset statusChangedAt)
	{
		Status = status;
		Notes = NormalizeOptional(notes, nameof(notes), AdmissionCrmConsts.MaxNotesLength);
		StatusChangedAt = statusChangedAt;
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
