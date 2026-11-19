using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Versioned definition policy. Historical definitions and captured answers are never upgraded in place.</summary>
internal static class AdmissionCoreFieldPolicy
{
	public const int CurrentVersion = 1;

	public static void UpgradeDraft(CreateUpdateAdmissionFormDto input)
	{
		CreateUpdateAdmissionFormSectionDto? safeSection = null;
		foreach (var spec in RequiredFields(input))
		{
			foreach (var section in input.Sections.ToList())
			{
				foreach (var field in section.Fields.Where(field => IsBinding(field, spec.Key)).ToList())
				{
					field.BindingKey = spec.Key;
					field.FieldType = spec.Type;
					field.Required = spec.Required;
					field.ReadOnly = false;
					field.VisibilityRuleJson = null;
					field.RequiredRuleJson = null;
					var keepChoices = spec.Key == Relationship.Key
						? HasExactChoices(field.OptionsJson, ["father", "mother", "guardian"])
						: AdmissionJourneyIdentityConfiguration.CompatibleType(new() { FieldType = field.FieldType, OptionsJson = field.OptionsJson }, spec.Key);
					if (spec.Options != null && !keepChoices)
						field.OptionsJson = spec.Options;
					var stepIsHidden = section.StepId.HasValue && input.Sections.Any(other =>
						other.StepId == section.StepId && !Blank(other.StepVisibilityRuleJson));
					if (!section.IsRepeatable && Blank(section.VisibilityRuleJson) && Blank(section.StepVisibilityRuleJson) && !stepIsHidden) continue;
					safeSection ??= CreateCoreSection(input);
					section.Fields.Remove(field);
					safeSection.Fields.Add(field);
				}
			}
		}
		if (safeSection != null) input.Sections.Insert(0, safeSection);
	}

	public static void EnsureParentEmail(int? version, string? email)
	{
		if (!version.HasValue) return;
		if (string.IsNullOrWhiteSpace(email) || !MailAddress.TryCreate(email.Trim(), out var parsed) || !string.Equals(parsed.Address, email.Trim(), StringComparison.OrdinalIgnoreCase))
			throw new BusinessException(AdmissionApplicationErrorCodes.ValidationFailed, "Enter a valid parent email address.").WithData("Fields", "Guardian.Email");
	}

	private sealed record CoreField(string Key, string Code, string Label, AdmissionFormFieldType Type, bool Required = true, string? Options = null);
	private static readonly CoreField[] Identity =
	[
		new("Applicant.FirstName", "APPLICANT_FIRST_NAME", "First name", AdmissionFormFieldType.Text),
		new("Applicant.LastName", "APPLICANT_LAST_NAME", "Last name", AdmissionFormFieldType.Text, false),
		new("Guardian.FirstName", "PARENT_NAME", "Parent name", AdmissionFormFieldType.Text),
		new("Guardian.Mobile", "PARENT_PHONE", "Parent contact number", AdmissionFormFieldType.Phone),
		new("Guardian.Email", "PARENT_EMAIL", "Parent email", AdmissionFormFieldType.Email)
	];
	private static readonly CoreField Relationship = new("Inquiry.ParentRelationship", "PARENT_RELATIONSHIP", "Relationship to student", AdmissionFormFieldType.Select, true,
		"[{\"value\":\"father\",\"label\":\"Father\"},{\"value\":\"mother\",\"label\":\"Mother\"},{\"value\":\"guardian\",\"label\":\"Guardian\"}]");
	private static readonly CoreField[] RegistrationIdentity =
	[
		new("Applicant.DateOfBirth", "DATE_OF_BIRTH", "Date of birth", AdmissionFormFieldType.Date),
		new("Applicant.Gender", "APPLICANT_GENDER", "Gender", AdmissionFormFieldType.Select, true,
			"[{\"value\":\"Male\",\"label\":\"Male\"},{\"value\":\"Female\",\"label\":\"Female\"},{\"value\":\"Other\",\"label\":\"Other\"}]")
	];

	public static void PrepareNew(CreateUpdateAdmissionFormDto input)
	{
		input.CoreFieldPolicyVersion = CurrentVersion;
		// Only reserved historical starter codes carry known identity semantics. Arbitrary labels never do.
		foreach (var field in input.Sections.SelectMany(section => section.Fields))
		{
			var binding = AdmissionInquiryConfiguredFields.ReservedBinding(field.Code, field.BindingKey);
			if (binding == null) continue;
			field.BindingType = AdmissionFormBindingType.System;
			field.BindingKey = binding;
		}
		var specs = RequiredFields(input).ToList();
		// A new definition has no captured answers. The original historical definition stays intact.
		if (input.Purpose == AdmissionFormPurpose.Inquiry || input.Inheritance?.Enabled != true)
			foreach (var section in input.Sections)
				section.Fields.RemoveAll(field => field.BindingType == AdmissionFormBindingType.System && string.Equals(field.BindingKey, "Applicant.Name", StringComparison.OrdinalIgnoreCase));
		var missing = specs.Where(spec => !input.Sections.SelectMany(section => section.Fields).Any(field => IsBinding(field, spec.Key))).ToList();
		if (missing.Count == 0) return;
		var core = CreateCoreSection(input);
		var usedCodes = input.Sections.SelectMany(section => section.Fields).Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var spec in missing)
			core.Fields.Add(new()
			{
				Code = UniqueCode(spec.Code, usedCodes), Label = spec.Label,
				BindingType = AdmissionFormBindingType.System, BindingKey = spec.Key,
				FieldType = spec.Type, Required = spec.Required,
				OptionsJson = spec.Options, DisplayOrder = core.Fields.Count
			});
		input.Sections.Insert(0, core);
	}

	public static IReadOnlyList<string> Validate(CreateUpdateAdmissionFormDto input, IReadOnlySet<string>? guaranteedInheritedKeys = null)
	{
		if (!input.CoreFieldPolicyVersion.HasValue) return [];
		if (input.CoreFieldPolicyVersion != CurrentVersion) return ["Admissions.FormUnsupportedCoreFieldPolicy"];
		var errors = new List<string>();
		foreach (var spec in RequiredFields(input))
		{
			var matches = input.Sections.SelectMany(section => section.Fields.Select(field => (section, field)))
				.Where(item => IsBinding(item.field, spec.Key)).ToList();
			if (matches.Count == 0 && guaranteedInheritedKeys?.Contains(spec.Key) == true) continue;
			if (matches.Count != 1)
			{
				errors.Add("Admissions.FormCoreFieldRequired:" + spec.Key);
				continue;
			}
			var (section, field) = matches[0];
			var stepIsHidden = section.StepId.HasValue && input.Sections.Any(other =>
				other.StepId == section.StepId && !Blank(other.StepVisibilityRuleJson));
			if (!string.Equals(field.BindingKey, spec.Key, StringComparison.Ordinal) ||
				field.FieldType != spec.Type || field.Required != spec.Required || field.ReadOnly || section.IsRepeatable ||
				!Blank(field.VisibilityRuleJson) || !Blank(field.RequiredRuleJson) || !Blank(section.VisibilityRuleJson) || !Blank(section.StepVisibilityRuleJson) ||
				stepIsHidden ||
				(spec.Key == Relationship.Key && !HasExactChoices(field.OptionsJson, ["father", "mother", "guardian"])))
				errors.Add("Admissions.FormCoreFieldProtected:" + spec.Key);
		}
		return errors;
	}

	public static void EnsureValid(CreateUpdateAdmissionFormDto input, IReadOnlySet<string>? inherited = null)
	{
		var errors = Validate(input, inherited);
		if (errors.Count > 0) throw new BusinessException(AdmissionFormErrorCodes.InvalidDefinition).WithData("Errors", string.Join(";", errors));
	}

	public static bool IsGuaranteed(AdmissionFormDto form, string bindingKey) => form.Sections.Any(section =>
		!section.IsRepeatable && Blank(section.VisibilityRuleJson) && Blank(section.StepVisibilityRuleJson) &&
		(!section.StepId.HasValue || !form.Sections.Any(other => other.StepId == section.StepId && !Blank(other.StepVisibilityRuleJson))) &&
		section.Fields.Any(field => field.BindingType == AdmissionFormBindingType.System &&
			string.Equals(field.BindingKey, bindingKey, StringComparison.Ordinal) && field.Required && !field.ReadOnly &&
			Blank(field.VisibilityRuleJson) && Blank(field.RequiredRuleJson) &&
			AdmissionJourneyIdentityConfiguration.CompatibleType(new() { FieldType = field.FieldType, OptionsJson = field.OptionsJson }, bindingKey)));

	private static IEnumerable<CoreField> RequiredFields(CreateUpdateAdmissionFormDto input) => input.Purpose == AdmissionFormPurpose.Inquiry ? Identity.Append(Relationship) :
		input.Inheritance?.Enabled == true ? RegistrationIdentity : Identity.Concat(RegistrationIdentity);
	private static bool IsBinding(CreateUpdateAdmissionFormFieldDto field, string key) => field.BindingType == AdmissionFormBindingType.System && string.Equals(field.BindingKey, key, StringComparison.OrdinalIgnoreCase);
	private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

	private static CreateUpdateAdmissionFormSectionDto CreateCoreSection(CreateUpdateAdmissionFormDto input) => new()
	{
		Code = UniqueCode("CORE_DETAILS", input.Sections.Select(section => section.Code).ToHashSet(StringComparer.OrdinalIgnoreCase)),
		Name = "Student and parent details",
		DisplayOrder = input.Sections.Count == 0 ? 0 : input.Sections.Min(section => section.DisplayOrder) - 1
	};

	private static string UniqueCode(string proposed, ISet<string> used)
	{
		var code = proposed;
		for (var suffix = 2; !used.Add(code); suffix++) code = proposed + "_" + suffix;
		return code;
	}

	private static bool HasExactChoices(string? options, string[] expected)
	{
		try
		{
			using var json = JsonDocument.Parse(options ?? "null");
			return json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() == expected.Length &&
				json.RootElement.EnumerateArray().Select(option => option.GetProperty("value").GetString()).ToHashSet(StringComparer.Ordinal).SetEquals(expected);
		}
		catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException) { return false; }
	}
}
