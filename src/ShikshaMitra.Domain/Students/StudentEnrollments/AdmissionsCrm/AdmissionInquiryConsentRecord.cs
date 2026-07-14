using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionInquiryConsentRecord : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid FormTemplateId { get; private set; }
	public string TemplateNameSnapshot { get; private set; } = string.Empty;
	public string ConsentTextSnapshot { get; private set; } = string.Empty;
	public string PrivacyNoticeUrl { get; private set; } = string.Empty;
	public string ConsentVersion { get; private set; } = string.Empty;
	public string CommunicationPreferencesJson { get; private set; } = "[]";
	public DateTime AcceptedAt { get; private set; }
	public string? IpAddress { get; private set; }
	public string? UserAgent { get; private set; }

	protected AdmissionInquiryConsentRecord()
	{
	}

	public AdmissionInquiryConsentRecord(
		Guid id,
		Guid? tenantId,
		Guid leadId,
		Guid formTemplateId,
		string templateNameSnapshot,
		string consentTextSnapshot,
		string privacyNoticeUrl,
		string consentVersion,
		string communicationPreferencesJson,
		DateTime acceptedAt,
		string? ipAddress,
		string? userAgent)
		: base(id)
	{
		TenantId = tenantId;
		LeadId = leadId;
		FormTemplateId = formTemplateId;
		TemplateNameSnapshot = Check.NotNullOrWhiteSpace(
			templateNameSnapshot,
			nameof(templateNameSnapshot),
			maxLength: AdmissionCrmConsts.MaxTemplateNameLength);
		ConsentTextSnapshot = Check.NotNullOrWhiteSpace(
			consentTextSnapshot,
			nameof(consentTextSnapshot),
			maxLength: AdmissionCrmConsts.MaxConsentTextLength);
		PrivacyNoticeUrl = Check.NotNullOrWhiteSpace(
			privacyNoticeUrl,
			nameof(privacyNoticeUrl),
			maxLength: AdmissionCrmConsts.MaxUrlLength);
		ConsentVersion = Check.NotNullOrWhiteSpace(
			consentVersion,
			nameof(consentVersion),
			maxLength: AdmissionCrmConsts.MaxConsentVersionLength);
		CommunicationPreferencesJson = Check.NotNullOrWhiteSpace(
			communicationPreferencesJson,
			nameof(communicationPreferencesJson),
			maxLength: AdmissionCrmConsts.MaxCommunicationPreferencesJsonLength);
		AcceptedAt = acceptedAt;
		IpAddress = NormalizeOptional(ipAddress, nameof(ipAddress), AdmissionCrmConsts.MaxIpAddressLength);
		UserAgent = NormalizeOptional(userAgent, nameof(userAgent), AdmissionCrmConsts.MaxUserAgentLength);
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
