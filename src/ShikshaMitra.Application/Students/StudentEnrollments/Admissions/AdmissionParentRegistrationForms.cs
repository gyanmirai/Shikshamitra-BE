using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionParentRegistrationForms(AdmissionParentRegistrationData data, AdmissionApplicationFormValidator validator,
	AdmissionFormMasterDataValidator master, AdmissionInquiryAcademicOptionsService options) : ITransientDependency
{
	internal static List<AdmissionFormValueDto> Merge(List<AdmissionFormValueDto> saved, List<AdmissionFormValueDto> patch)
	{
		var result = saved.ToDictionary(value => value.FieldCode, StringComparer.OrdinalIgnoreCase);
		foreach (var value in patch) result[value.FieldCode] = value;
		return result.Values.ToList();
	}
	internal static List<AdmissionFormValueDto> VisibleProposals(AdmissionParentForm form, List<AdmissionFormValueDto> patch, AdmissionApplicationFormValidator validator)
	{
		// Reverting a hidden controller can hide another answer. Settle the complete
		// dependency chain before retaining proposals for the audited staff command.
		for (var pass = 0; pass <= form.Definition.Sections.Sum(section => section.Fields.Count); pass++)
		{
			var before = JsonSerializer.Serialize(patch);
			patch = VisibleProposalPass(form, patch, validator);
			if (JsonSerializer.Serialize(patch) == before) return patch;
		}
		throw AdmissionParentRegistrationData.Invalid("The conditional correction could not be resolved. Ask the school to review the requested fields.");
	}
	private static List<AdmissionFormValueDto> VisibleProposalPass(AdmissionParentForm form, List<AdmissionFormValueDto> patch, AdmissionApplicationFormValidator validator)
	{
		var merged = Merge(form.Values, patch);
		var before = form.Values.ToDictionary(value => value.FieldCode, value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
		var after = merged.ToDictionary(value => value.FieldCode, value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
		var fields = AdmissionEffectiveFormResolver.MaterializeFields(form.Definition);
		foreach (var section in AdmissionEffectiveFormResolver.MaterializeSections(form.Definition))
		{
			var sectionFields = fields.Where(field => field.SectionId == section.Id).ToList();
			var repeated = form.Definition.Sections.Single(item => item.Id == section.Id).IsRepeatable;
			var newRows = sectionFields.ToDictionary(field => field.Code, field => Cells(after.GetValueOrDefault(field.Code), repeated), StringComparer.OrdinalIgnoreCase);
			var count = Math.Max(1, newRows.Values.Select(row => row.Count).DefaultIfEmpty(0).Max());
			var visible = new List<IReadOnlySet<string>>();
			for (var index = 0; index < count; index++)
			{
				var row = new Dictionary<string, string>(after, StringComparer.OrdinalIgnoreCase);
				foreach (var field in sectionFields) row[field.Code] = newRows[field.Code].ElementAtOrDefault(index) ?? "";
				visible.Add(validator.GetVisibleFieldCodes(sectionFields, [section], row));
			}
			foreach (var value in patch.Where(value => sectionFields.Any(field => field.Code == value.FieldCode)))
			{
				var cells = newRows[value.FieldCode];
				var original = Cells(before.GetValueOrDefault(value.FieldCode), repeated);
				var changed = false;
				for (var index = 0; index < cells.Count; index++)
					if (!visible[index].Contains(value.FieldCode) && cells[index] != (original.ElementAtOrDefault(index) ?? ""))
					{ cells[index] = original.ElementAtOrDefault(index) ?? ""; changed = true; }
				if (changed) value.ValueJson = repeated ? JsonSerializer.Serialize(JsonSerializer.Serialize(cells)) : JsonSerializer.Serialize(cells.FirstOrDefault() ?? "");
			}
		}
		return patch.Where(value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson) != before.GetValueOrDefault(value.FieldCode, "")).ToList();
	}
	internal static HashSet<string> Editable(AdmissionParentForm form, AdmissionParentRegistration grant, AdmissionApplicant applicant) => form.Definition.Sections.SelectMany(section => section.Fields)
		.Where(field => AdmissionParentRegistrationSchema.CanWriteFor(field, JsonSerializer.Deserialize<AdmissionParentRecipientDto>(grant.RecipientJson)!.Relationship, applicant)).Select(field => field.Code)
		.Where(code => grant.Purpose == "initial" || JsonSerializer.Deserialize<List<string>>(grant.FieldCodesJson)!.Contains(code, StringComparer.OrdinalIgnoreCase))
		.ToHashSet(StringComparer.OrdinalIgnoreCase);
	internal async Task ValidateAsync(AdmissionApplication application, AdmissionParentForm form, List<AdmissionFormValueDto> merged,
		bool complete, IReadOnlySet<string> editable, IReadOnlySet<string>? changed = null)
	{
		var definition = JsonSerializer.Deserialize<AdmissionFormDto>(JsonSerializer.Serialize(form.Definition))!;
		if (!complete)
			foreach (var section in definition.Sections)
			{
				section.MinEntries = 0;
				foreach (var field in section.Fields) { field.Required = false; field.RequiredRuleJson = null; }
			}
		var values = merged.ToDictionary(value => value.FieldCode, value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
		var fields = AdmissionEffectiveFormResolver.MaterializeFields(definition);
		var sections = AdmissionEffectiveFormResolver.MaterializeSections(definition);
		var experience = AdmissionEffectiveFormResolver.Experience(definition);
		var visible = AdmissionEffectiveFormResolver.VisibleFieldCodes(definition, values, validator);
		sections = sections.Where(section => fields.Any(field => field.SectionId == section.Id && editable.Contains(field.Code) && visible.Contains(field.Code))).ToList();
		if (changed != null) CheckVisibleChanges(definition, form.Values, merged, changed, validator);
		var validation = validator.Validate(fields, sections, values, experience, editable);
		var offering = await data.Repo<AdmissionOffering>().GetAsync(application.AdmissionOfferingId);
		var cycle = await data.Repo<AdmissionCycle>().GetAsync(application.AdmissionCycleId);
		await master.ValidateAsync(fields, sections, values, cycle.AcademicYearId, validation, experience,
			(key, parent) => options.ForPublicOfferingAsync(key, offering, parent), editable);
		if (validation.Errors.Count > 0) throw new BusinessException(complete ? AdmissionParentRegistrationErrors.Incomplete : AdmissionParentRegistrationErrors.Invalid,
			"Review the highlighted form answers before continuing.").WithData("FieldErrors", validation.FieldErrors);
	}
	internal static void CheckVisibleChanges(AdmissionFormDto definition, List<AdmissionFormValueDto> saved, List<AdmissionFormValueDto> merged,
		IReadOnlySet<string> changed, AdmissionApplicationFormValidator validator)
	{
		var before = saved.ToDictionary(value => value.FieldCode, value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
		var after = merged.ToDictionary(value => value.FieldCode, value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
		var fields = AdmissionEffectiveFormResolver.MaterializeFields(definition);
		foreach (var section in AdmissionEffectiveFormResolver.MaterializeSections(definition))
		{
			var sectionFields = fields.Where(field => field.SectionId == section.Id).ToList();
			var repeated = definition.Sections.Single(item => item.Id == section.Id).IsRepeatable;
			var oldRows = sectionFields.ToDictionary(field => field.Code, field => Cells(before.GetValueOrDefault(field.Code), repeated), StringComparer.OrdinalIgnoreCase);
			var newRows = sectionFields.ToDictionary(field => field.Code, field => Cells(after.GetValueOrDefault(field.Code), repeated), StringComparer.OrdinalIgnoreCase);
			var count = Math.Max(1, newRows.Values.Select(row => row.Count).DefaultIfEmpty(0).Max());
			for (var index = 0; index < count; index++)
			{
				var rowValues = new Dictionary<string, string>(after, StringComparer.OrdinalIgnoreCase);
				foreach (var field in sectionFields) rowValues[field.Code] = newRows[field.Code].ElementAtOrDefault(index) ?? "";
				var rowVisible = validator.GetVisibleFieldCodes(sectionFields, [section], rowValues);
				foreach (var field in sectionFields.Where(field => changed.Contains(field.Code)))
					if (!rowVisible.Contains(field.Code) && (oldRows[field.Code].ElementAtOrDefault(index) ?? "") != (newRows[field.Code].ElementAtOrDefault(index) ?? ""))
						throw AdmissionParentRegistrationData.Invalid("A hidden answer cannot be changed.");
			}
		}
	}
	private static List<string> Cells(string? value, bool repeated)
	{
		if (!repeated) return [value ?? ""];
		if (string.IsNullOrWhiteSpace(value)) return [];
		try { using var json = JsonDocument.Parse(value); return json.RootElement.ValueKind == JsonValueKind.Array ? json.RootElement.EnumerateArray()
			.Select(cell => cell.ValueKind == JsonValueKind.String ? cell.GetString() ?? "" : cell.GetRawText()).ToList() : []; }
		catch (JsonException) { return []; }
	}
	internal static void CheckPatch(AdmissionParentForm form, HashSet<string> writable, List<AdmissionFormValueDto> values)
	{
		if (values == null || values.Count > 500 || values.Select(value => value.FieldCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count)
			throw AdmissionParentRegistrationData.Invalid("Provide one answer per field.");
		foreach (var value in values)
		{
			if (string.IsNullOrWhiteSpace(value.FieldCode) || !writable.Contains(value.FieldCode))
				throw AdmissionParentRegistrationData.Invalid("This answer is not editable through this registration request.");
			if (value.ValueJson?.Length > 8 * 1024 * 1024) throw AdmissionParentRegistrationData.Invalid("This answer exceeds the supported size.");
		}
	}
	internal async Task<AdmissionParentDefinitionDto> ProjectAsync(AdmissionApplication application, AdmissionParentForm form, HashSet<string> editable)
	{
		var definition = AdmissionParentRegistrationSchema.Project(form.Definition, editable, form.Values);
		var offering = await data.Repo<AdmissionOffering>().GetAsync(application.AdmissionOfferingId);
		foreach (var field in definition.Sections.SelectMany(section => section.Fields))
			if (AdmissionFormMasterDataValidator.TrySource(AdmissionAcademicFormOptions.OptionsJson(field.Code, field.FieldType, field.OptionsJson), out var key, out var parentField))
			{
				if (!AdmissionAcademicFormOptions.IsAcademic(key)) throw AdmissionParentRegistrationData.Invalid("This public choice source is unavailable.");
				field.OptionsSourceJson = JsonSerializer.Serialize(new { source = key, parentField });
				field.OptionsJson = AdmissionAcademicFormOptions.SerializeOptions(await options.ForPublicOfferingAsync(key, offering));
			}
		return definition;
	}
}
