using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

/// <summary>Current custom values use the first activated Registration as their baseline.
/// Returning submissions are review evidence and never implicitly overwrite that baseline.</summary>
[RemoteService(false)]
public class StudentRecordCustomDetailsService(StudentRecordRegistrationReader reader,
	IRepository<StudentRecordCustomProfile, Guid> profiles, AdmissionApplicationFormValidator validator,
	StudentRecordsAccessService access, StudentRecordsPolicyResolver policies, AdmissionFormMasterDataValidator masterDataValidator,
	StudentRecordCurrentRuleReader currentRules, StudentRecordFieldOptionsService fieldOptions)
	: ApplicationService, ITransientDependency
{
	private static JsonElement Null => JsonSerializer.SerializeToElement<object?>(null);
	public async Task<List<StudentRecordRegistrationSourceDto>> ReadAsync(Guid studentId)
	{
		var sources = await reader.GetAsync(studentId);
		var stored = await profiles.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId);
		var current = BaselineValues(sources);
		var readOnlyIdentities = sources.SelectMany(source => CustomFields(source).Where(field => field.ReadOnly || !CanEditField(field))
			.Select(field => Identity(source, field))).ToHashSet(StringComparer.Ordinal);
		if (stored != null)
			foreach (var item in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stored.ValuesJson) ?? []) current[item.Key] = item.Value;
		foreach (var source in sources)
		{
			source.CurrentValues = new(source.OriginalValues, StringComparer.OrdinalIgnoreCase);
			foreach (var field in CustomFields(source))
			{
				source.CapturedFieldCodes.Add(field.Code);
				var key = Identity(source, field);
				source.CurrentValues[field.Code] = current.GetValueOrDefault(key, Null);
				if (!readOnlyIdentities.Contains(key)) source.EditableFieldCodes.Add(field.Code);
				if (!source.IsBaseline && source.OriginalValues.TryGetValue(field.Code, out var submitted) &&
					!Equal(source.CurrentValues[field.Code], submitted) && HasValue(submitted))
					source.Differences.Add(new() { FieldCode = field.Code, Label = field.Label, CurrentValue = source.CurrentValues[field.Code],
						SubmittedValue = submitted, CanReview = !readOnlyIdentities.Contains(key) });
			}
		}
		if (sources.Any(x => StudentRecordCurrentRuleReader.ReferencedFields(x).Count > 0) && !await IsMaskedAsync(studentId))
			await currentRules.RefreshAsync(studentId, sources);
		return sources;
	}
	public async Task<bool> IsMaskedAsync(Guid studentId, string? recordedSourceSet = null)
	{
		var record = await access.EnsureReadableAsync(studentId);
		var roles = await access.GetDatabaseRoleNamesAsync();
		if (record.Access != "Full" || StudentRecordVisibility.IsMasked(record.Policy, "custom-details", roles)) return true;
		var visibility = await reader.GetSourceVisibilityAsync(studentId);
		if (!visibility.BaselineReadable) return true;
		if (recordedSourceSet != null)
		{
			var recorded = JsonSerializer.Deserialize<StudentRecordCustomDetailsPayload>(recordedSourceSet, StudentRecordPayloadService.JsonOptions);
			if (recorded?.Sources == null || recorded.Sources.Any(x => x == null || !visibility.SubmissionIds.Contains(x.SubmissionId))) return true;
		}
		foreach (var year in await reader.GetSourceYearIdsAsync(studentId))
			if (StudentRecordVisibility.IsMasked((await policies.ResolveAsync(year)).Policy, "custom-details", roles)) return true;
		return false;
	}
	public async Task<string> CaptureAsync(Guid studentId, string? sourceSetJson = null)
	{
		if (await IsMaskedAsync(studentId)) throw new BusinessException("Students:Records:RestrictedFields");
		var sources = await ReadAsync(studentId);
		if (sourceSetJson != null)
		{
			var expected = JsonSerializer.Deserialize<StudentRecordCustomDetailsPayload>(sourceSetJson, StudentRecordPayloadService.JsonOptions)!;
			var ids = expected.Sources.Select(x => x.SubmissionId).ToHashSet();
			sources = sources.Where(x => ids.Contains(x.SubmissionId)).ToList();
			if (sources.Count != ids.Count) throw new BusinessException("Students:Records:RestrictedFields");
		}
		return Serialize(ToPayload(sources));
	}
	public async Task<string> ValidateAndNormalizeAsync(Guid studentId, JsonElement payload, bool recordedSourceSet = false, bool registrationForm = false)
	{
		await access.EnsureWritableAsync(studentId);
		if (!registrationForm && await IsMaskedAsync(studentId)) throw new BusinessException("Students:Records:RestrictedFields");
		StudentRecordCustomDetailsPayload input;
		try { input = JsonSerializer.Deserialize<StudentRecordCustomDetailsPayload>(payload.GetRawText(), StudentRecordPayloadService.JsonOptions)!; }
		catch (JsonException) { throw new BusinessException("Students:Records:InvalidPayload"); }
		if (input?.Sources == null || input.Sources.Any(x => x == null || x.Values == null)) throw new BusinessException("Students:Records:InvalidPayload");
		var sources = await ReadAsync(studentId);
		if (registrationForm) await LazyServiceProvider.LazyGetRequiredService<StudentRecordFormProjection>().PopulateAsync(studentId, sources, true);
		if (recordedSourceSet) sources = sources.Where(x => input.Sources.Any(p => p.SubmissionId == x.SubmissionId)).ToList();
		if (sources.Count == 0 || input.Sources.Count != sources.Count || input.Sources.Select(x => x.SubmissionId).Distinct().Count() != sources.Count)
			throw new BusinessException("Students:Records:InvalidCustomSource");
		var changes = new Dictionary<string, JsonElement>();
		foreach (var source in sources)
		{
			var proposed = input.Sources.SingleOrDefault(x => x.SubmissionId == source.SubmissionId);
			if (proposed == null || proposed.ApplicationId != source.ApplicationId || proposed.FormInstanceId != source.FormInstanceId)
				throw new BusinessException("Students:Records:InvalidCustomSource");
			var fields = Fields(source).ToList();
			if (proposed.Values.Count != fields.Count || fields.Any(x => !proposed.Values.ContainsKey(x.Code)))
				throw new BusinessException("Students:Records:InvalidCustomField");
			foreach (var field in fields)
			{
				var value = proposed.Values[field.Code];
				if (Equal(value, source.CurrentValues[field.Code])) continue;
				if (!Editable(source).Contains(field.Code)) throw new BusinessException("Students:Records:OwnedFieldChange").WithData("Field", field.Code);
				EnsureShape(source, field, value);
				if (AdmissionFormMasterDataValidator.TrySource(field.OptionsJson, out var sourceKey, out _) && sourceKey.Equals("StudentSiblings", StringComparison.OrdinalIgnoreCase))
				{
					var ids = SiblingIds(value).Except(SiblingIds(source.CurrentValues[field.Code])).Distinct().ToArray();
					if (!await fieldOptions.AreSiblingIdsAllowedAsync(ids)) throw new BusinessException("Students:Records:InvalidCustomValues");
				}
				var key = Identity(source, field);
				if (changes.TryGetValue(key, out var other) && !Equal(value, other))
					throw new BusinessException("Students:Records:ConflictingCustomValues");
				changes[key] = value;
			}
		}
		// A shared compatible field has one current value, even across multiple submitted versions.
		foreach (var source in sources)
		{
			var changedFields = Fields(source).Where(x => changes.ContainsKey(Identity(source, x))).ToList();
			var previousValues = new Dictionary<string, JsonElement>(source.CurrentValues, StringComparer.OrdinalIgnoreCase);
			foreach (var field in changedFields) source.CurrentValues[field.Code] = changes[Identity(source, field)];
			if (changedFields.Count == 0) continue;
			foreach (var field in changedFields)
				if (!await fieldOptions.AreChangedMasterOptionsAllowedAsync(source, field, previousValues.GetValueOrDefault(field.Code, Null)))
					throw new BusinessException("Students:Records:InvalidCustomValues").WithData("Field", field.Code);
			var definition = source.Definition!;
			if (definition.Sections.Any(section => section.IsRepeatable && RowCount(section, previousValues) != RowCount(section, source.CurrentValues) &&
				section.Fields.Any(field => field.FieldType != AdmissionFormFieldType.Static && !Editable(source).Contains(field.Code))))
				throw new BusinessException("Students:Records:OwnedFieldChange");
			var fieldDefinitions = Fields(source).Where(f => !source.FormMaskedFieldCodes.Contains(f.Code)).Select(ToDefinition).ToList();
			var sectionDefinitions = definition.Sections.Select(ToSection).ToList();
			var stringValues = source.CurrentValues.ToDictionary(x => x.Key, x => Unwrap(x.Value), StringComparer.OrdinalIgnoreCase);
			var visibleBefore = VisiblePaths(definition, previousValues);
			var visibleAfter = VisiblePaths(definition, source.CurrentValues);
			var changedPaths = ChangedPaths(definition, changedFields, previousValues, source.CurrentValues);
			if (changedPaths.Any(x => !visibleAfter.Contains(x)))
				throw new BusinessException("Students:Records:HiddenCustomFieldChange");
			var experience = new AdmissionFormExperienceMetadata();
			foreach (var section in definition.Sections) experience.Sections[section.Code] = new()
			{ IsRepeatable = section.IsRepeatable, MinEntries = section.MinEntries, MaxEntries = section.MaxEntries };
			var validation = validator.Validate(fieldDefinitions, sectionDefinitions, stringValues, experience);
			// New sibling IDs use Records eligibility and Identity rules; retained unavailable IDs remain historical values.
			await masterDataValidator.ValidateAsync(fieldDefinitions.Where(f => !AdmissionFormMasterDataValidator.TrySource(f.OptionsJson, out var key, out _) ||
				!key.Equals("StudentSiblings", StringComparison.OrdinalIgnoreCase)), sectionDefinitions, stringValues, source.AcademicYearId, validation, experience);
			var relevant = changedPaths.Concat(visibleAfter.Except(visibleBefore))
				.Concat(RequiredPaths(definition, source.CurrentValues).Except(RequiredPaths(definition, previousValues))).ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (registrationForm)
			{
				var required = RequiredPaths(definition, source.CurrentValues);
				foreach (var section in definition.Sections)
					foreach (var field in section.Fields.Where(f => !source.FormMaskedFieldCodes.Contains(f.Code) && f.FieldType is AdmissionFormFieldType.MultiSelect or AdmissionFormFieldType.AddressGroup))
						for (var index = 0; index < (section.IsRepeatable ? RowCount(section, source.CurrentValues) : 1); index++)
						{
							var path = section.IsRepeatable ? $"{field.Code}[{index}]" : field.Code;
							var value = source.CurrentValues.GetValueOrDefault(field.Code, Null);
							if (required.Contains(path) && !HasStructuredValue(section.IsRepeatable ? ArrayItem(value, index) : value))
								validation.FieldErrors.TryAdd(path, $"{field.Code} is required.");
						}
			}
			var changedDependentFields = definition.Sections.SelectMany(s => s.Fields).Where(f =>
				AdmissionFormMasterDataValidator.TrySource(f.OptionsJson, out _, out var parent) && changedFields.Any(c => c.Code == parent)).Select(f => f.Code).ToHashSet();
			var errors = validation.FieldErrors.Where(x => relevant.Contains(x.Key) || changedDependentFields.Any(f => x.Key == f || x.Key.StartsWith(f + "[", StringComparison.Ordinal)) ||
				definition.Sections.Any(s => s.Code == x.Key && s.Fields.Any(f => changedFields.Any(c => c.Code == f.Code) ||
					relevant.Any(p => p == f.Code || p.StartsWith(f.Code + "[", StringComparison.Ordinal))))).ToDictionary(x => x.Key, x => x.Value);
			if (errors.Count > 0) throw new BusinessException("Students:Records:InvalidCustomValues").WithData("FieldErrors", errors);
		}
		return Serialize(ToPayload(sources, registrationForm));
		IEnumerable<AdmissionFormFieldDto> Fields(StudentRecordRegistrationSourceDto source) => registrationForm ? StudentRecordFormProjection.Fields(source) : CustomFields(source);
		List<string> Editable(StudentRecordRegistrationSourceDto source) => registrationForm ? source.FormEditableFieldCodes : source.EditableFieldCodes;
	}
	public async Task ApplyAsync(Guid studentId, string json, bool registrationForm = false)
	{
		// Revalidate captured provenance and server definitions at application time as well as request creation.
		var normalized = await ValidateAndNormalizeAsync(studentId, JsonSerializer.Deserialize<JsonElement>(json), recordedSourceSet: true, registrationForm: registrationForm);
		var input = JsonSerializer.Deserialize<StudentRecordCustomDetailsPayload>(normalized, StudentRecordPayloadService.JsonOptions)!;
		var sources = (await ReadAsync(studentId)).Where(x => input.Sources.Any(p => p.SubmissionId == x.SubmissionId)).ToList();
		// Display redaction must never become stored current truth for fields this caller cannot edit.
		var baselineValues = BaselineValues(sources);
		if (registrationForm)
		{
			await LazyServiceProvider.LazyGetRequiredService<StudentRecordFormProjection>().PopulateAsync(studentId, sources, true);
			await LazyServiceProvider.LazyGetRequiredService<StudentRecordFormMapping>().ApplyAsync(studentId, sources, input);
		}
		var stored = await profiles.FindAsync(x => x.StudentId == studentId && x.TenantId == CurrentTenant.Id);
		var values = stored == null ? baselineValues : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stored.ValuesJson) ?? [];
		foreach (var baseline in baselineValues) values.TryAdd(baseline.Key, baseline.Value);
		foreach (var source in sources)
		{
			var proposed = input.Sources.Single(x => x.SubmissionId == source.SubmissionId);
			foreach (var field in registrationForm ? StudentRecordFormProjection.Fields(source).Where(f => source.FormEditableFieldCodes.Contains(f.Code)) : CustomFields(source))
				values[Identity(source, field)] = proposed.Values[field.Code];
		}
		var sourceJson = MergeProvenance(stored?.SourcesJson, JsonSerializer.Serialize(sources.Select(x => new { x.ApplicationId, x.AcademicYearId, x.FormInstanceId, x.SubmissionId, x.FormDefinitionId, x.FormFamilyId, x.FormVersion })));
		var schemaJson = MergeProvenance(stored?.SchemaJson, JsonSerializer.Serialize(sources.Select(x => new { x.SubmissionId, x.Definition })));
		var valuesJson = JsonSerializer.Serialize(values);
		if (stored == null) await profiles.InsertAsync(new StudentRecordCustomProfile(GuidGenerator.Create(), CurrentTenant.Id,
			studentId, sources.FirstOrDefault(x => x.IsBaseline)?.SubmissionId ?? await reader.GetBaselineSubmissionIdAsync(studentId) ??
			throw new BusinessException("Students:Records:InvalidCustomSource"), sourceJson, schemaJson, valuesJson));
		else { stored.UpdateCurrent(sourceJson, schemaJson, valuesJson); await profiles.UpdateAsync(stored); }
	}

	internal static StudentRecordCustomDetailsPayload ToPayload(List<StudentRecordRegistrationSourceDto> sources, bool registrationForm = false) => new()
	{
		Sources = sources.Select(source => new StudentRecordCustomSourcePayload
		{
			ApplicationId = source.ApplicationId, FormInstanceId = source.FormInstanceId, SubmissionId = source.SubmissionId,
			Values = (registrationForm ? StudentRecordFormProjection.Fields(source) : CustomFields(source)).ToDictionary(x => x.Code, x => source.CurrentValues.GetValueOrDefault(x.Code, Null), StringComparer.OrdinalIgnoreCase)
		}).ToList()
	};
	internal static Dictionary<string, JsonElement> BaselineValues(List<StudentRecordRegistrationSourceDto> sources)
	{
		var baseline = sources.FirstOrDefault(x => x.IsBaseline);
		return baseline == null ? [] : CustomFields(baseline).ToDictionary(x => Identity(baseline, x), x => baseline.OriginalValues.GetValueOrDefault(x.Code, Null));
	}
	internal static IEnumerable<AdmissionFormFieldDto> CustomFields(StudentRecordRegistrationSourceDto source) =>
		source.Definition?.Sections.SelectMany(x => x.Fields).Where(x => x.FieldType != AdmissionFormFieldType.Static &&
			(x.BindingType == AdmissionFormBindingType.Custom || x.BindingKey != null &&
			 AdmissionAdditionalStudentFieldCatalog.Metadata.TryGetValue(x.BindingKey, out var metadata) && metadata.Destination == "capturedAdmissionDetails")) ?? [];
	internal static string Identity(StudentRecordRegistrationSourceDto source, AdmissionFormFieldDto field) =>
		$"{source.FormFamilyId:D}:{field.Code}:{field.FieldType}:{source.Definition!.Sections.First(x => x.Id == field.SectionId).IsRepeatable}";
	internal static bool CanEditField(AdmissionFormFieldDto field)
	{
		if (field.FieldType == AdmissionFormFieldType.File || field.BindingKey == "Applicant.ProfilePhoto") return false;
		if (AdmissionFormMasterDataValidator.TrySource(field.OptionsJson, out var source, out _) && !AdmissionFormDataSourceRegistry.IsAllowed(source)) return false;
		try
		{
			using var validation = JsonDocument.Parse(field.ValidationJson ?? "{}");
			return !validation.RootElement.TryGetProperty("format", out var format) || format.ValueKind == JsonValueKind.String && format.GetString() != "photo";
		}
		catch (JsonException) { return false; }
	}
	internal static bool Equal(JsonElement left, JsonElement right) => StudentRecordPayloadService.Canonical(left.GetRawText()) == StudentRecordPayloadService.Canonical(right.GetRawText());
	internal static bool HasValue(JsonElement value) => value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) &&
		(value.ValueKind != JsonValueKind.String || !string.IsNullOrWhiteSpace(value.GetString()));
	private static bool HasStructuredValue(JsonElement value) => value.ValueKind switch
	{
		JsonValueKind.Array => value.EnumerateArray().Any(HasStructuredValue),
		JsonValueKind.Object => value.EnumerateObject().Any(item => HasStructuredValue(item.Value)),
		_ => HasValue(value)
	};
	private static IEnumerable<Guid> SiblingIds(JsonElement value)
	{
		if (value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().SelectMany(SiblingIds);
		if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())) return [];
		if (value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) && id != Guid.Empty) return [id];
		throw new BusinessException("Students:Records:InvalidCustomValues");
	}
	internal static string Unwrap(JsonElement value) => value.ValueKind switch
	{
		JsonValueKind.Null or JsonValueKind.Undefined => "", JsonValueKind.String => value.GetString() ?? "", _ => value.GetRawText()
	};
	private static string Serialize(StudentRecordCustomDetailsPayload value) => StudentRecordPayloadService.Canonical(JsonSerializer.Serialize(value, StudentRecordPayloadService.JsonOptions));
	private HashSet<string> VisiblePaths(AdmissionFormDto definition, IReadOnlyDictionary<string, JsonElement> values)
	{
		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var section in definition.Sections)
		{
			var count = section.IsRepeatable ? RowCount(section, values) : 1;
			for (var index = 0; index < count; index++)
			{
				var row = values.ToDictionary(x => x.Key, x => Unwrap(x.Value), StringComparer.OrdinalIgnoreCase);
				if (section.IsRepeatable)
					foreach (var field in section.Fields) row[field.Code] = Unwrap(ArrayItem(values.GetValueOrDefault(field.Code, Null), index));
				foreach (var code in validator.GetVisibleFieldCodes(section.Fields.Select(ToDefinition), [ToSection(section)], row))
					result.Add(section.IsRepeatable ? $"{code}[{index}]" : code);
			}
		}
		return result;
	}
	private static HashSet<string> ChangedPaths(AdmissionFormDto definition, List<AdmissionFormFieldDto> fields,
		IReadOnlyDictionary<string, JsonElement> before, IReadOnlyDictionary<string, JsonElement> after)
	{
		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var field in fields)
		{
			var section = definition.Sections.First(x => x.Id == field.SectionId);
			if (!section.IsRepeatable) { result.Add(field.Code); continue; }
			// Removing an entire repeatable row is explicit. Keeping a row must retain its hidden answers.
			for (var index = 0; index < RowCount(section, after); index++)
				if (!Equal(ArrayItem(before.GetValueOrDefault(field.Code, Null), index), ArrayItem(after.GetValueOrDefault(field.Code, Null), index)))
					result.Add($"{field.Code}[{index}]");
		}
		return result;
	}
	private HashSet<string> RequiredPaths(AdmissionFormDto definition, IReadOnlyDictionary<string, JsonElement> values)
	{
		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var section in definition.Sections)
		{
			var fields = section.Fields.Where(f => f.Required || !string.IsNullOrWhiteSpace(f.RequiredRuleJson)).Select(f =>
				new AdmissionFormFieldDefinition(f.Id, f.FormDefinitionId, f.SectionId, f.Code, f.Label, f.FieldType, f.BindingType, f.BindingKey,
					false, f.ReadOnly, f.DisplayOrder, f.Placeholder, f.HelpText, f.DefaultValue, f.ValidationJson,
					f.Required ? f.VisibilityRuleJson : CombineRules(f.VisibilityRuleJson, f.RequiredRuleJson), f.OptionsJson)).ToList();
			var count = section.IsRepeatable ? RowCount(section, values) : 1;
			for (var index = 0; index < count; index++)
			{
				var row = values.ToDictionary(x => x.Key, x => Unwrap(x.Value), StringComparer.OrdinalIgnoreCase);
				if (section.IsRepeatable)
					foreach (var field in section.Fields) row[field.Code] = Unwrap(ArrayItem(values.GetValueOrDefault(field.Code, Null), index));
				foreach (var code in validator.GetVisibleFieldCodes(fields, [ToSection(section)], row))
					result.Add(section.IsRepeatable ? $"{code}[{index}]" : code);
			}
		}
		return result;
	}
	private static int RowCount(AdmissionFormSectionDto section, IReadOnlyDictionary<string, JsonElement> values) =>
		section.Fields.Select(x => values.GetValueOrDefault(x.Code, Null)).Where(x => x.ValueKind == JsonValueKind.Array)
			.Select(x => x.GetArrayLength()).DefaultIfEmpty(0).Max();
	private static JsonElement ArrayItem(JsonElement array, int index) => array.ValueKind == JsonValueKind.Array && index < array.GetArrayLength() ? array[index] : Null;
	internal static string MergeProvenance(string? previous, string current)
	{
		var rows = (JsonNode.Parse(previous ?? "[]")?.AsArray() ?? new JsonArray()).ToList();
		foreach (var row in JsonNode.Parse(current)!.AsArray())
		{
			var key = row!["SubmissionId"]?.ToString() ?? row["submissionId"]?.ToString();
			rows.RemoveAll(x => (x!["SubmissionId"]?.ToString() ?? x["submissionId"]?.ToString()) == key);
			rows.Add(row.DeepClone());
		}
		return new JsonArray(rows.Select(x => x?.DeepClone()).ToArray()).ToJsonString();
	}
	private static void EnsureShape(StudentRecordRegistrationSourceDto source, AdmissionFormFieldDto field, JsonElement value)
	{
		if (value.ValueKind == JsonValueKind.Null) return;
		if (source.Definition!.Sections.First(x => x.Id == field.SectionId).IsRepeatable)
		{
			if (value.ValueKind != JsonValueKind.Array) throw new BusinessException("Students:Records:InvalidCustomValues").WithData("Field", field.Code);
			foreach (var item in value.EnumerateArray()) EnsureScalar(field, item);
		}
		else EnsureScalar(field, value);
	}
	private static void EnsureScalar(AdmissionFormFieldDto field, JsonElement value)
	{
		var valid = value.ValueKind == JsonValueKind.Null || field.FieldType switch
		{
			AdmissionFormFieldType.Number => value.ValueKind is JsonValueKind.Number or JsonValueKind.String,
			AdmissionFormFieldType.Checkbox => value.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.String,
			AdmissionFormFieldType.MultiSelect => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String),
			AdmissionFormFieldType.AddressGroup => value.ValueKind == JsonValueKind.Object,
			_ => value.ValueKind == JsonValueKind.String
		};
		if (!valid) throw new BusinessException("Students:Records:InvalidCustomValues").WithData("Field", field.Code);
	}
	private static AdmissionFormFieldDefinition ToDefinition(AdmissionFormFieldDto f) => new(f.Id, f.FormDefinitionId, f.SectionId,
		f.Code, f.Label, f.FieldType, f.BindingType, f.BindingKey, f.Required, f.ReadOnly, f.DisplayOrder,
		f.Placeholder, f.HelpText, f.DefaultValue, f.ValidationJson, f.VisibilityRuleJson, f.OptionsJson, f.RequiredRuleJson);
	private static AdmissionFormSectionDefinition ToSection(AdmissionFormSectionDto s) => new(s.Id, s.FormDefinitionId,
		s.Code, s.Name, s.DisplayOrder, CombineRules(s.VisibilityRuleJson, s.StepVisibilityRuleJson), s.StepId, s.StepCode, s.StepName, s.StepDisplayOrder, s.StepVisibilityRuleJson);
	private static string? CombineRules(string? section, string? step) => string.IsNullOrWhiteSpace(section) ? step : string.IsNullOrWhiteSpace(step) ? section :
		JsonSerializer.Serialize(new { @operator = "ALL", conditions = new[] { JsonSerializer.Deserialize<JsonElement>(section), JsonSerializer.Deserialize<JsonElement>(step) } });
}
