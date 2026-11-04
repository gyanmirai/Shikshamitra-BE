using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionParentRegistrationSchema
{
	private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
	{
		"Applicant.Email", "Applicant.Mobile", "Guardian.FirstName", "Guardian.LastName", "Guardian.Mobile", "Guardian.Email",
		"Guardian.Relationship", "Guardian.AlternateName"
	};
	internal static bool IsExposed(AdmissionFormFieldDto field)
	{
		var key = field.BindingKey ?? "";
		if (key.StartsWith("Identity.", StringComparison.OrdinalIgnoreCase) || key.StartsWith("Inquiry.", StringComparison.OrdinalIgnoreCase) ||
			key.StartsWith("Finance.", StringComparison.OrdinalIgnoreCase) || key.StartsWith("Family.", StringComparison.OrdinalIgnoreCase) && key != "Family.IsSingleParentChild" ||
			key.StartsWith("Academic.", StringComparison.OrdinalIgnoreCase) && key is not ("Academic.SecondLanguage" or "Academic.LearningSupportNeeds")) return false;
		return !AdmissionFormMasterDataValidator.TrySource(field.OptionsJson, out var source, out _) || source != "StudentSiblings";
	}
	internal static bool CanWrite(AdmissionFormFieldDto field) => IsExposed(field) && !field.ReadOnly && !Protected.Contains(field.BindingKey ?? "") &&
		(field.BindingType != AdmissionFormBindingType.System || string.IsNullOrEmpty(field.BindingKey) ||
		AdmissionFormBindingRegistry.Metadata.ContainsKey(field.BindingKey));
	internal static bool CanWriteFor(AdmissionFormFieldDto field, string recipientRelationship, AdmissionApplicant applicant)
	{
		if (!CanWrite(field)) return false;
		var key = field.BindingKey ?? "";
		if (key is "Father.Name" or "Father.Email" or "Father.Mobile" or "Mother.Name" or "Mother.Email" or "Mother.Mobile")
		{
			var role = key.StartsWith("Father.", StringComparison.Ordinal) ? "father" : "mother";
			return (role != recipientRelationship || key.EndsWith(".Mobile", StringComparison.Ordinal)) && !applicant.ExistingStudentId.HasValue && AdmissionSiblingParentSelection.ReadConfirmation(applicant) == null;
		}
		return true;
	}
	internal static string? RuntimeBinding(string? key) => key != null && (key.StartsWith("Address.", StringComparison.Ordinal) ||
		key.StartsWith("PermanentAddress.", StringComparison.Ordinal) || key is "Applicant.Name" or "Applicant.FirstName" or "Applicant.MiddleName" or
		"Applicant.LastName" or "Applicant.Gender" or "Applicant.ProfilePhoto") ? key : null;
	internal static List<string> ExpandCorrectionFields(AdmissionFormDto definition, List<string> requested, IReadOnlySet<string> writable)
	{
		var fields = definition.Sections.SelectMany(section => section.Fields.Select(field => (section, field)))
			.ToDictionary(item => item.field.Code, StringComparer.OrdinalIgnoreCase);
		var selected = requested.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var queue = new Queue<string>(requested);
		while (queue.TryDequeue(out var code))
		{
			var (section, field) = fields[code];
			var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var rule in new[] { section.VisibilityRuleJson, section.StepVisibilityRuleJson, field.VisibilityRuleJson, field.RequiredRuleJson })
			{
				if (string.IsNullOrWhiteSpace(rule)) continue;
				using var document = JsonDocument.Parse(rule);
				Read(document.RootElement, dependencies);
			}
			if (AdmissionFormMasterDataValidator.TrySource(field.OptionsJson, out _, out var parentField) && parentField != null) dependencies.Add(parentField);
			foreach (var dependency in dependencies)
				if (writable.Contains(dependency) && selected.Add(dependency)) queue.Enqueue(dependency);
		}
		return selected.ToList();

		static void Read(JsonElement node, HashSet<string> result)
		{
			if (node.ValueKind != JsonValueKind.Object) return;
			if (node.TryGetProperty("field", out var field) && field.ValueKind == JsonValueKind.String) result.Add(field.GetString()!);
			if (node.TryGetProperty("conditions", out var children) && children.ValueKind == JsonValueKind.Array)
				foreach (var child in children.EnumerateArray()) Read(child, result);
		}
	}
	internal static void EnsureCorrectionReachable(AdmissionFormDto definition, List<AdmissionFormValueDto> saved, List<string> requested,
		IReadOnlySet<string> writable, AdmissionApplicationFormValidator validator)
	{
		var values = saved.ToDictionary(value => value.FieldCode, value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
		var visible = AdmissionEffectiveFormResolver.VisibleFieldCodes(definition, values, validator);
		var evaluator = new AdmissionWorkflowConditionEvaluator();
		foreach (var section in definition.Sections)
			foreach (var field in section.Fields.Where(field => requested.Contains(field.Code, StringComparer.OrdinalIgnoreCase) && !visible.Contains(field.Code)))
				if (!Possible(section.VisibilityRuleJson) || !Possible(section.StepVisibilityRuleJson) || !Possible(field.VisibilityRuleJson))
					throw AdmissionParentRegistrationData.Invalid("A requested answer is hidden by a protected or read-only control. Update that control through the staff form before requesting this correction.");
		bool Possible(string? rule)
		{
			if (string.IsNullOrWhiteSpace(rule)) return true;
			using var document = JsonDocument.Parse(rule);
			return Read(document.RootElement);
		}
		bool Read(JsonElement node)
		{
			if (node.TryGetProperty("conditions", out var children) && children.ValueKind == JsonValueKind.Array)
				return node.GetProperty("operator").GetString()?.Equals("ANY", StringComparison.OrdinalIgnoreCase) == true
					? children.EnumerateArray().Any(Read) : children.EnumerateArray().All(Read);
			return node.TryGetProperty("field", out var code) && writable.Contains(code.GetString() ?? "") || evaluator.Evaluate(node.GetRawText(), values);
		}
	}

	internal static AdmissionParentDefinitionDto Project(AdmissionFormDto definition, IReadOnlySet<string> editable, List<AdmissionFormValueDto> values)
	{
		var result = JsonSerializer.Deserialize<AdmissionParentDefinitionDto>(JsonSerializer.Serialize(definition))!;
		var fields = definition.Sections.SelectMany(section => section.Fields).ToDictionary(field => field.Code, StringComparer.OrdinalIgnoreCase);
		var omitted = fields.Values.Where(field => !IsExposed(field)).ToDictionary(field => field.Code, field =>
			AdmissionApplicationValueConverter.Unwrap(values.FirstOrDefault(value => value.FieldCode == field.Code)?.ValueJson), StringComparer.Ordinal);
		foreach (var section in result.Sections)
		{
			section.VisibilityRuleJson = AdmissionPublicInquirySchema.ProjectRule(section.VisibilityRuleJson, omitted);
			section.StepVisibilityRuleJson = AdmissionPublicInquirySchema.ProjectRule(section.StepVisibilityRuleJson, omitted);
			section.Fields = section.Fields.Where(field => IsExposed(fields[field.Code])).ToList();
			foreach (var field in section.Fields)
			{
				field.ReadOnly = !editable.Contains(field.Code);
				field.VisibilityRuleJson = AdmissionPublicInquirySchema.ProjectRule(field.VisibilityRuleJson, omitted);
				field.RequiredRuleJson = AdmissionPublicInquirySchema.ProjectRule(field.RequiredRuleJson, omitted);
				field.RuntimeBindingKey = RuntimeBinding(fields[field.Code].BindingKey);
			}
		}
		result.Sections.RemoveAll(section => section.Fields.Count == 0);
		return result;
	}
}
