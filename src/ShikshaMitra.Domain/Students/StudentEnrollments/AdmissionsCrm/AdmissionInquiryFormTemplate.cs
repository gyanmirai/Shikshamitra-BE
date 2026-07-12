using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionInquiryFormTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string TemplateName { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public AdmissionInquiryChannel Channel { get; private set; }
	public bool IsPublic { get; private set; }
	public bool IsActive { get; private set; }
	public string? ConsentText { get; private set; }
	public string? PrivacyNoticeUrl { get; private set; }
	public string FieldsJson { get; private set; } = "[]";
	public string LocalizationsJson { get; private set; } = "{}";

	protected AdmissionInquiryFormTemplate()
	{
	}

	public AdmissionInquiryFormTemplate(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string templateName,
		string? description,
		AdmissionInquiryChannel channel,
		bool isPublic,
		string? consentText,
		string? privacyNoticeUrl,
		string fieldsJson,
		string localizationsJson)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		IsActive = true;
		Update(templateName, description, channel, isPublic, consentText, privacyNoticeUrl, fieldsJson, localizationsJson);
	}

	public void Update(
		string templateName,
		string? description,
		AdmissionInquiryChannel channel,
		bool isPublic,
		string? consentText,
		string? privacyNoticeUrl,
		string fieldsJson,
		string localizationsJson)
	{
		TemplateName = Check.NotNullOrWhiteSpace(
			templateName,
			nameof(templateName),
			maxLength: AdmissionCrmConsts.MaxTemplateNameLength);
		Description = NormalizeOptional(description, nameof(description), AdmissionCrmConsts.MaxDescriptionLength);
		Channel = channel;
		IsPublic = isPublic;
		ConsentText = NormalizeOptional(consentText, nameof(consentText), AdmissionCrmConsts.MaxConsentTextLength);
		PrivacyNoticeUrl = NormalizeOptional(privacyNoticeUrl, nameof(privacyNoticeUrl), AdmissionCrmConsts.MaxUrlLength);
		SetFieldsJson(fieldsJson);
		SetLocalizationsJson(localizationsJson);

		if (IsPublic && string.IsNullOrWhiteSpace(ConsentText))
		{
			throw new BusinessException("AdmissionCrm:PublicFormConsentRequired")
				.WithData("TemplateName", TemplateName);
		}

		if (IsPublic && string.IsNullOrWhiteSpace(PrivacyNoticeUrl))
		{
			throw new BusinessException("AdmissionCrm:PublicFormPrivacyNoticeRequired")
				.WithData("TemplateName", TemplateName);
		}
	}

	private void SetLocalizationsJson(string localizationsJson)
	{
		var value = Check.NotNullOrWhiteSpace(localizationsJson, nameof(localizationsJson), AdmissionCrmConsts.MaxFieldsJsonLength);
		using var parsed = JsonDocument.Parse(value);
		if (parsed.RootElement.ValueKind != JsonValueKind.Object) throw new BusinessException("AdmissionCrm:FormLocalizationsMustBeObject");
		foreach (var property in parsed.RootElement.EnumerateObject())
		{
			if (property.Name is not ("en" or "gu" or "hi")) throw new BusinessException("AdmissionCrm:UnsupportedFormLanguage").WithData("Language", property.Name);
		}
		LocalizationsJson = value;
	}

	public void Activate()
	{
		IsActive = true;
	}

	public void Deactivate()
	{
		IsActive = false;
	}

	private void SetFieldsJson(string fieldsJson)
	{
		if (string.IsNullOrWhiteSpace(fieldsJson))
		{
			throw new BusinessException("AdmissionCrm:FormFieldsRequired");
		}

		Check.Length(fieldsJson, nameof(fieldsJson), AdmissionCrmConsts.MaxFieldsJsonLength);

		using var parsed = JsonDocument.Parse(fieldsJson);
		if (parsed.RootElement.ValueKind != JsonValueKind.Array)
		{
			throw new BusinessException("AdmissionCrm:FormFieldsMustBeArray");
		}

		if (parsed.RootElement.GetArrayLength() == 0)
		{
			throw new BusinessException("AdmissionCrm:FormFieldsRequired");
		}

		FieldsJson = fieldsJson;
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
