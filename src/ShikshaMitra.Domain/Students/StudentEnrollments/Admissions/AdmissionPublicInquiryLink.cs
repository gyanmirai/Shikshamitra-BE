using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionPublicInquiryLink : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AdmissionCycleId { get; private set; }
	public Guid? CounsellorAssignmentProfileId { get; private set; }
	public Guid? InquiryFormDefinitionId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string TokenHash { get; private set; } = string.Empty;
	public AdmissionInquirySource Source { get; private set; }
	public string? SourceDetail { get; private set; }
	public DateTime? ExpiresAtUtc { get; private set; }
	public bool IsActive { get; private set; } = true;

	private AdmissionPublicInquiryLink() { }

	public AdmissionPublicInquiryLink(Guid id, Guid? tenantId, Guid cycleId, Guid? assignmentProfileId,
		string name, string tokenHash, AdmissionInquirySource source, string? sourceDetail, DateTime? expiresAtUtc,
		Guid? inquiryFormDefinitionId = null)
		: base(id)
	{
		TenantId = tenantId;
		AdmissionCycleId = Check.NotDefaultOrNull<Guid>(cycleId, nameof(cycleId));
		if (assignmentProfileId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(assignmentProfileId));
		CounsellorAssignmentProfileId = assignmentProfileId;
		if (inquiryFormDefinitionId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(inquiryFormDefinitionId));
		InquiryFormDefinitionId = inquiryFormDefinitionId;
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), 128).Trim();
		TokenHash = Check.NotNullOrWhiteSpace(tokenHash, nameof(tokenHash), 64);
		if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));
		Source = source;
		SourceDetail = sourceDetail?.Trim();
		ExpiresAtUtc = expiresAtUtc?.ToUniversalTime();
	}

	public bool CanAccept(DateTime nowUtc) => IsActive &&
		(!ExpiresAtUtc.HasValue || ExpiresAtUtc.Value > nowUtc.ToUniversalTime());

	public void Revoke() => IsActive = false;

	public void SetInquiryForm(Guid formDefinitionId)
	{
		if (!IsActive) throw new BusinessException(AdmissionIntakeProfileErrorCodes.InvalidConfiguration);
		InquiryFormDefinitionId = Check.NotDefaultOrNull<Guid>(formDefinitionId, nameof(formDefinitionId));
	}
}
