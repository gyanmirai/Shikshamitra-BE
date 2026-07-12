using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmIdentityConsent : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid PersonId { get; private set; }
	public string Purpose { get; private set; } = string.Empty;
	public string TextVersion { get; private set; } = string.Empty;
	public string Language { get; private set; } = "en";
	public string Actor { get; private set; } = string.Empty;
	public string Channel { get; private set; } = string.Empty;
	public bool IsGranted { get; private set; }
	public DateTime RecordedAt { get; private set; }
	public DateTime? WithdrawnAt { get; private set; }
	public string? WithdrawalReason { get; private set; }

	protected AdmissionCrmIdentityConsent()
	{
	}

	public AdmissionCrmIdentityConsent(
		Guid id, Guid? tenantId, Guid academicYearId, Guid leadId, Guid personId,
		string purpose, string textVersion, string language, string actor, string channel,
		bool isGranted, DateTime recordedAt) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		PersonId = personId;
		Purpose = Check.NotNullOrWhiteSpace(purpose, nameof(purpose), AdmissionCrmConsts.MaxPurposeLength);
		TextVersion = Check.NotNullOrWhiteSpace(textVersion, nameof(textVersion), AdmissionCrmConsts.MaxConsentVersionLength);
		Language = Check.NotNullOrWhiteSpace(language, nameof(language), AdmissionCrmConsts.MaxLanguageCodeLength);
		Actor = Check.NotNullOrWhiteSpace(actor, nameof(actor), AdmissionCrmConsts.MaxNameLength);
		Channel = Check.NotNullOrWhiteSpace(channel, nameof(channel), AdmissionCrmConsts.MaxNameLength);
		IsGranted = isGranted;
		RecordedAt = recordedAt.ToUniversalTime();
	}

	public void Withdraw(string reason, DateTime at)
	{
		if (!IsGranted || WithdrawnAt.HasValue)
		{
			throw new BusinessException("AdmissionCrm:IdentityConsentNotActive");
		}
		WithdrawalReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		WithdrawnAt = at.ToUniversalTime();
		IsGranted = false;
	}
}
