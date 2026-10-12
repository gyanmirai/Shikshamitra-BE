using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionIntakeSetup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public Guid InquiryIntakeProfileId { get; private set; }
	public Guid CounsellorAssignmentProfileId { get; private set; }
	public bool IsArchived { get; private set; }
	public DateTime? ArchivedTime { get; private set; }
	public Guid? ArchivedByUserId { get; private set; }

	private AdmissionIntakeSetup() { }

	public AdmissionIntakeSetup(Guid id, Guid? tenantId, string name, Guid inquiryIntakeProfileId,
		Guid counsellorAssignmentProfileId) : base(id)
	{
		TenantId = tenantId;
		InquiryIntakeProfileId = Check.NotDefaultOrNull<Guid>(inquiryIntakeProfileId,
			nameof(inquiryIntakeProfileId));
		CounsellorAssignmentProfileId = Check.NotDefaultOrNull<Guid>(counsellorAssignmentProfileId,
			nameof(counsellorAssignmentProfileId));
		if (inquiryIntakeProfileId == counsellorAssignmentProfileId)
			throw new ArgumentException("Inquiry and counsellor assignment profiles must be different.");
		UpdateName(name);
	}

	public void UpdateName(string name)
	{
		if (IsArchived)
			throw new BusinessException(AdmissionIntakeProfileErrorCodes.SetupArchived);
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), 128).Trim();
	}

	public void Archive(DateTime archivedTime, Guid? archivedByUserId)
	{
		if (IsArchived) return;
		IsArchived = true;
		ArchivedTime = archivedTime.ToUniversalTime();
		ArchivedByUserId = archivedByUserId;
	}

	public void Restore()
	{
		IsArchived = false;
		ArchivedTime = null;
		ArchivedByUserId = null;
	}
}
