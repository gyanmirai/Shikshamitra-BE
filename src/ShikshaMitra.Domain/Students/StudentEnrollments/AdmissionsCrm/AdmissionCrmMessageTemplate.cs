using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmMessageTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string TemplateName { get; private set; } = string.Empty;
	public AdmissionCrmMessageChannel Channel { get; private set; }
	public string Purpose { get; private set; } = string.Empty;
	public string? Subject { get; private set; }
	public string Body { get; private set; } = string.Empty;
	public string VariablesJson { get; private set; } = "[]";
	public AdmissionCrmMessageTemplateStatus Status { get; private set; }
	public bool IsActive { get; private set; }

	protected AdmissionCrmMessageTemplate()
	{
	}

	public AdmissionCrmMessageTemplate(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string templateName,
		AdmissionCrmMessageChannel channel,
		string purpose,
		string? subject,
		string body,
		string variablesJson)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Status = AdmissionCrmMessageTemplateStatus.Draft;
		IsActive = true;
		Update(templateName, channel, purpose, subject, body, variablesJson);
	}

	public void Update(
		string templateName,
		AdmissionCrmMessageChannel channel,
		string purpose,
		string? subject,
		string body,
		string variablesJson)
	{
		TemplateName = Check.NotNullOrWhiteSpace(
			templateName,
			nameof(templateName),
			maxLength: AdmissionCrmConsts.MaxTemplateNameLength);
		Channel = channel;
		Purpose = Check.NotNullOrWhiteSpace(
			purpose,
			nameof(purpose),
			maxLength: AdmissionCrmConsts.MaxPurposeLength);
		Subject = NormalizeOptional(subject, nameof(subject), AdmissionCrmConsts.MaxMessageSubjectLength);
		Body = Check.NotNullOrWhiteSpace(
			body,
			nameof(body),
			maxLength: AdmissionCrmConsts.MaxMessageBodyLength);
		SetVariablesJson(variablesJson);

		if (Channel == AdmissionCrmMessageChannel.Email && string.IsNullOrWhiteSpace(Subject))
		{
			throw new BusinessException("AdmissionCrm:EmailTemplateSubjectRequired")
				.WithData("TemplateName", TemplateName);
		}
	}

	public void UpdateStatus(AdmissionCrmMessageTemplateStatus status, bool isActive)
	{
		Status = status;
		IsActive = isActive;
	}

	private void SetVariablesJson(string variablesJson)
	{
		if (string.IsNullOrWhiteSpace(variablesJson))
		{
			variablesJson = "[]";
		}

		Check.Length(
			variablesJson,
			nameof(variablesJson),
			AdmissionCrmConsts.MaxTemplateVariablesJsonLength);

		using var parsed = JsonDocument.Parse(variablesJson);
		if (parsed.RootElement.ValueKind != JsonValueKind.Array)
		{
			throw new BusinessException("AdmissionCrm:MessageTemplateVariablesMustBeArray");
		}

		VariablesJson = variablesJson;
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
