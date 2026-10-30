using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionFormInheritanceMetadataStore
{
	private const string PolicyKey = "AdmissionFormInheritanceV1";
	private const string IdentitiesKey = "AdmissionFormFieldIdentitiesV1";
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionFormInheritancePolicyDto? ReadPolicy(AdmissionFormDefinition form)
	{
		var json = form.GetProperty<string>(PolicyKey);
		if (string.IsNullOrWhiteSpace(json)) return null;
		try
		{
			var policy = JsonSerializer.Deserialize<AdmissionFormInheritancePolicyDto>(json, JsonOptions) ?? throw InvalidPolicy();
			Validate(policy);
			return policy;
		}
		catch (JsonException) { throw InvalidPolicy(); }
	}

	public static void Write(AdmissionFormDefinition form, CreateUpdateAdmissionFormDto input)
	{
		if (input.Inheritance?.Enabled == true && input.Purpose != AdmissionFormPurpose.Registration) throw InvalidPolicy();
		var policy = input.Inheritance;
		var previous = ReadPolicy(form);
		if (previous?.Enabled == true && previous.Sources.Count > 0 && policy?.Enabled != true) throw InvalidPolicy();
		if (policy != null)
		{
			Validate(policy);
			if (previous?.Enabled == true && previous.Sources.Any(source => policy.Sources.All(next => next.InquiryFormDefinitionId != source.InquiryFormDefinitionId))) throw InvalidPolicy();
		}
		form.SetProperty(PolicyKey, policy == null ? null : JsonSerializer.Serialize(policy, JsonOptions));
		var existing = ReadIdentities(form);
		var identities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var field in input.Sections.SelectMany(section => section.Fields).Where(field => field.BindingType == AdmissionFormBindingType.Custom))
		{
			var code = field.Code.Trim().ToUpperInvariant();
			var known = existing.GetValueOrDefault(code);
			// An edited label/code is not authority to impersonate a Student binding or another question.
			var proposed = string.IsNullOrWhiteSpace(field.FieldIdentity) ? null : field.FieldIdentity.Trim();
			if (!string.IsNullOrWhiteSpace(proposed) && !proposed.StartsWith("custom:", StringComparison.OrdinalIgnoreCase)) throw InvalidPolicy();
			if (known != null && proposed != null && !string.Equals(known, proposed, StringComparison.OrdinalIgnoreCase)) throw InvalidPolicy();
			var stableFieldId = field.Id.HasValue && field.Id.Value != Guid.Empty ? field.Id.Value : Guid.NewGuid();
			identities[code] = known ?? proposed ?? "custom:" + stableFieldId.ToString("D");
		}
		form.SetProperty(IdentitiesKey, JsonSerializer.Serialize(identities, JsonOptions));
	}

	public static void Validate(AdmissionFormInheritancePolicyDto policy)
	{
		if (policy.Sources == null || policy.Sources.Count > 100 || policy.Sources.Any(source => source == null || source.InquiryFormDefinitionId == Guid.Empty) ||
			policy.Sources.Select(source => source.InquiryFormDefinitionId).Distinct().Count() != policy.Sources.Count) throw InvalidPolicy();
		foreach (var source in policy.Sources)
		{
			if (source.FieldMappings == null || source.Layouts == null || source.RequiredFieldIdentities == null ||
				source.FieldMappings.Count > 500 || source.Layouts.Count > 500 || source.RequiredFieldIdentities.Count > 500 ||
				source.RequiredFieldIdentities.Any(string.IsNullOrWhiteSpace) ||
				source.FieldMappings.Any(mapping => mapping == null || string.IsNullOrWhiteSpace(mapping.SourceFieldCode) || string.IsNullOrWhiteSpace(mapping.RegistrationFieldCode)) ||
				source.Layouts.Any(layout => layout == null || string.IsNullOrWhiteSpace(layout.FieldIdentity) || layout.DisplayOrder < 0)) throw InvalidPolicy();
			if (source.FieldMappings.Select(mapping => mapping.SourceFieldCode.Trim().ToUpperInvariant()).Distinct().Count() != source.FieldMappings.Count ||
				source.Layouts.Select(layout => layout.FieldIdentity.Trim().ToUpperInvariant()).Distinct().Count() != source.Layouts.Count) throw InvalidPolicy();
		}
	}

	public static string Identity(AdmissionFormDefinition form, AdmissionFormFieldDefinition field)
	{
		if (field.BindingType == AdmissionFormBindingType.System) return "system:" + field.BindingKey?.ToUpperInvariant();
		return ReadIdentities(form).GetValueOrDefault(field.Code) ?? "custom:" + form.Code.ToUpperInvariant() + ":" + field.Code.ToUpperInvariant();
	}

	public static void Copy(AdmissionFormDefinition source, AdmissionFormDefinition target)
	{
		foreach (var key in new[] { PolicyKey, IdentitiesKey })
		{
			var value = source.GetProperty<string>(key);
			if (!string.IsNullOrWhiteSpace(value)) target.SetProperty(key, value);
		}
		if (ReadIdentities(source).Count == 0)
		{
			var identities = AdmissionFormMetadataStore.ReadExperience(source).Sections.Values.SelectMany(section => section.Fields.Keys)
				.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(code => code, code => "custom:" + source.Code.ToUpperInvariant() + ":" + code.ToUpperInvariant(), StringComparer.OrdinalIgnoreCase);
			if (identities.Count > 0) target.SetProperty(IdentitiesKey, JsonSerializer.Serialize(identities, JsonOptions));
		}
	}

	private static Dictionary<string, string> ReadIdentities(AdmissionFormDefinition form)
	{
		var json = form.GetProperty<string>(IdentitiesKey);
		if (string.IsNullOrWhiteSpace(json)) return new(StringComparer.OrdinalIgnoreCase);
		try { return new(JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? [], StringComparer.OrdinalIgnoreCase); }
		catch (JsonException) { throw InvalidPolicy(); }
	}

	private static BusinessException InvalidPolicy() => new(AdmissionFormInheritanceErrorCodes.InvalidPolicy,
		"Review the Inquiry inheritance settings before saving this form.");
}
