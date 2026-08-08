using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionInquiryFormSubmission : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid FormTemplateId { get; private set; }
	public string? SubmissionKey { get; private set; }
	public string TemplateNameSnapshot { get; private set; } = string.Empty;
	public string SubmittedFieldsJson { get; private set; } = "{}";
	public DateTime SubmittedAt { get; private set; }
	public string? IpAddress { get; private set; }
	public string? UserAgent { get; private set; }

	protected AdmissionInquiryFormSubmission()
	{
	}

	public AdmissionInquiryFormSubmission(
		Guid id,
		Guid? tenantId,
		Guid leadId,
		Guid formTemplateId,
		string templateNameSnapshot,
		string submittedFieldsJson,
		DateTime submittedAt,
		string? ipAddress,
		string? userAgent,
		string? submissionKey = null)
		: base(id)
	{
		TenantId = tenantId;
		LeadId = leadId;
		FormTemplateId = formTemplateId;
		SubmissionKey = NormalizeOptional(
			submissionKey,
			nameof(submissionKey),
			AdmissionCrmConsts.MaxSubmissionKeyLength);
		TemplateNameSnapshot = Check.NotNullOrWhiteSpace(
			templateNameSnapshot,
			nameof(templateNameSnapshot),
			maxLength: AdmissionCrmConsts.MaxTemplateNameLength);
		SetSubmittedFields(submittedFieldsJson);
		SubmittedAt = submittedAt;
		IpAddress = NormalizeOptional(ipAddress, nameof(ipAddress), AdmissionCrmConsts.MaxIpAddressLength);
		UserAgent = NormalizeOptional(userAgent, nameof(userAgent), AdmissionCrmConsts.MaxUserAgentLength);
	}

	private void SetSubmittedFields(string submittedFieldsJson)
	{
		if (string.IsNullOrWhiteSpace(submittedFieldsJson))
		{
			throw new BusinessException("AdmissionCrm:SubmittedFieldsRequired");
		}

		Check.Length(submittedFieldsJson, nameof(submittedFieldsJson), AdmissionCrmConsts.MaxSubmittedFieldsJsonLength);

		using var parsed = JsonDocument.Parse(submittedFieldsJson);
		if (parsed.RootElement.ValueKind != JsonValueKind.Object)
		{
			throw new BusinessException("AdmissionCrm:SubmittedFieldsMustBeObject");
		}

		SubmittedFieldsJson = submittedFieldsJson;
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
