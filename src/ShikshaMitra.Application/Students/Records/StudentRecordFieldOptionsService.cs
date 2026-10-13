using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

/// <summary>Dynamic choices retain the Registration's schema and Records' school scope.</summary>
[RemoteService(false)]
public class StudentRecordFieldOptionsService(AdmissionFormDataSourceRegistry registry, StudentDirectoryOptionsService directoryOptions,
	StudentRecordsEnrollmentScopeService scope, StudentRecordsAccessService access,
	IStudentRepository students, IRepository<StudentRecordLegacyOverride, Guid> overrides,
	IRepository<Standard, Guid> standards, StudentRecordsPolicyResolver policies) : ApplicationService, ITransientDependency
{
	public async Task PopulateAsync(StudentRecordRegistrationSourceDto source, bool canEdit, bool fullForm = false)
	{
		var cache = new Dictionary<string, List<AdmissionFormOptionDto>>();
		StudentDirectoryOptionsDto? permitted = null;
		foreach (var section in source.Definition!.Sections)
			foreach (var field in section.Fields)
			{
				if (fullForm && source.FormMaskedFieldCodes.Contains(field.Code)) continue;
				if (!AdmissionFormMasterDataValidator.TrySource(field.OptionsJson, out var key, out var parentField) || !AdmissionFormDataSourceRegistry.IsAllowed(key)) continue;
				if (!key.Equals("StudentSiblings", StringComparison.OrdinalIgnoreCase)) permitted ??= await directoryOptions.GetAsync(source.AcademicYearId);
				var rows = section.IsRepeatable ? Math.Max(RowCount(source.CurrentValues.GetValueOrDefault(field.Code)), RowCount(source.OriginalValues.GetValueOrDefault(field.Code))) : 1;
				var all = new List<AdmissionFormOptionDto>();
				for (var index = 0; index < rows; index++)
				{
					var selected = Selected(Row(source.CurrentValues.GetValueOrDefault(field.Code), section.IsRepeatable, index))
						.Concat(Selected(Row(source.OriginalValues.GetValueOrDefault(field.Code), section.IsRepeatable, index))).Distinct().ToList();
					var parent = Parent(source.CurrentValues, parentField, section.IsRepeatable, index);
					var originalParent = Parent(source.OriginalValues, parentField, section.IsRepeatable, index);
					var editable = canEdit && (fullForm ? source.FormEditableFieldCodes : source.EditableFieldCodes).Contains(field.Code);
					var options = await Cached(parent, editable);
					if (originalParent != parent)
						options = Merge(options, (await Cached(originalParent, false)));
					if (section.IsRepeatable) source.OptionsByFieldPath[$"{field.Code}[{index}]"] = options;
					all = Merge(all, options);
					async Task<List<AdmissionFormOptionDto>> Cached(Guid? parentId, bool available)
					{
						var cacheKey = $"{key}:{parentId}:{available}:{string.Join(',', selected.Order())}";
						if (!cache.TryGetValue(cacheKey, out var result))
							cache[cacheKey] = result = await ResolveAsync(source.AcademicYearId, key, parentId, selected, available, null, 0, 20, permitted);
						return result;
					}
				}
				source.OptionsByField[field.Code] = all;
			}
	}

	public async Task<List<AdmissionFormOptionDto>> QueryAsync(StudentRecordRegistrationSourceDto source, AdmissionFormFieldDto field, StudentRecordFieldOptionsInput input)
	{
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 21 || input.SearchTerm?.Length > 100 || input.SelectedIds == null ||
			input.SelectedIds.Count > 20 || input.SelectedIds.Contains(Guid.Empty)) throw new BusinessException("Students:Records:InvalidDirectoryQuery");
		if (!AdmissionFormMasterDataValidator.TrySource(field.OptionsJson, out var key, out _) || !AdmissionFormDataSourceRegistry.IsAllowed(key))
			throw new BusinessException("Students:Records:InvalidCustomField");
		var retained = Selected(source.CurrentValues.GetValueOrDefault(field.Code)).Concat(Selected(source.OriginalValues.GetValueOrDefault(field.Code))).ToHashSet();
		if (input.SelectedOnly && (!string.IsNullOrWhiteSpace(input.SearchTerm) || input.SkipCount != 0)) throw new BusinessException("Students:Records:InvalidDirectoryQuery");
		var result = await ResolveAsync(source.AcademicYearId, key, input.ParentId, input.SelectedIds, !input.SelectedOnly, input.SearchTerm, input.SkipCount, input.MaxResultCount);
		if (!input.SelectedOnly && input.SelectedIds.Any(id => !retained.Contains(id) && !result.Any(x => x.Value == id.ToString() && x.IsActive)))
			throw new BusinessException("Students:Records:InvalidCustomValues");
		return result;
	}

	public async Task<bool> AreSiblingIdsAllowedAsync(IReadOnlyCollection<Guid> ids)
	{
		var visible = await SiblingLabelsAsync(ids);
		return ids.All(id => visible.Any(option => option.Value == id.ToString() && option.IsActive));
	}
	internal async Task<bool> AreChangedMasterOptionsAllowedAsync(StudentRecordRegistrationSourceDto source, AdmissionFormFieldDto field, JsonElement previous)
	{
		if (!AdmissionFormMasterDataValidator.TrySource(field.OptionsJson, out var key, out var parentField) || key.Equals("StudentSiblings", StringComparison.OrdinalIgnoreCase)) return true;
		if (!AdmissionFormDataSourceRegistry.IsAllowed(key)) return false;
		var section = source.Definition!.Sections.Single(x => x.Id == field.SectionId);
		var current = source.CurrentValues[field.Code]; var count = section.IsRepeatable ? RowCount(current) : 1;
		for (var index = 0; index < count; index++)
		{
			var added = Selected(Row(current, section.IsRepeatable, index)).Except(Selected(Row(previous, section.IsRepeatable, index))).ToList();
			if (added.Count == 0) continue;
			var available = await ResolveAsync(source.AcademicYearId, key, Parent(source.CurrentValues, parentField, section.IsRepeatable, index), added, true, null, 0, 21);
			if (added.Any(id => !available.Any(option => option.Value == id.ToString() && option.IsActive))) return false;
		}
		return true;
	}

	private async Task<List<AdmissionFormOptionDto>> ResolveAsync(Guid year, string key, Guid? parent, List<Guid> selected, bool available,
		string? search, int skip, int max, StudentDirectoryOptionsDto? permitted = null)
	{
		key = AdmissionFormDataSourceRegistry.Metadata.First(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Key;
		if (key.Equals("StudentSiblings", StringComparison.OrdinalIgnoreCase))
		{
			var choices = new List<AdmissionFormOptionDto>();
			if (available && selected.Count == 0)
			{
				var query = await SiblingQueryAsync(); var roles = await access.GetDatabaseRoleNamesAsync(); var visibleYears = new List<Guid>();
				var years = await AsyncExecuter.ToListAsync(query.Select(x => x.AcademicYearId).Distinct());
				var visibleNull = false;
				foreach (var sourceYear in years)
					if (!(await policies.ResolveAsync(sourceYear)).Policy.IsMasked("Identity", roles))
						if (sourceYear.HasValue) visibleYears.Add(sourceYear.Value); else visibleNull = true;
				query = query.Where(x => x.AcademicYearId.HasValue && visibleYears.Contains(x.AcademicYearId.Value) || !x.AcademicYearId.HasValue && visibleNull);
				if (!string.IsNullOrWhiteSpace(search))
				{
					var term = search.Trim().ToLowerInvariant();
					query = query.Where(x => (x.FirstName + " " + x.LastName).ToLower().Contains(term) || x.GrNumber != null && x.GrNumber.ToLower().Contains(term));
				}
				var page = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ThenBy(x => x.Id).Skip(skip).Take(max)
					.Select(x => new { x.Id, x.FirstName, x.LastName, x.GrNumber }));
				choices = page.Select((x, i) => new AdmissionFormOptionDto
				{ Value = x.Id.ToString(), Label = $"{x.FirstName} {x.LastName}".Trim() + (string.IsNullOrWhiteSpace(x.GrNumber) ? "" : $" ({x.GrNumber})"), DisplayOrder = i }).ToList();
			}
			return Merge(choices, await SiblingLabelsAsync(selected));
		}
		permitted ??= await directoryOptions.GetAsync(year);
		var permittedStandardIds = permitted.Standards.Select(x => x.Id).ToArray();
		var allowedIds = key switch
		{
			"EducationBoards" => permitted.Boards.Select(x => x.Id).ToHashSet(),
			"EducationMediums" => permitted.Mediums.Where(x => x.ParentId == parent).Select(x => x.Id).ToHashSet(),
			"StandardsByMediumStage" => (await AsyncExecuter.ToListAsync((await standards.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
				x.EducationMediumStageId == parent && Enumerable.Contains(permittedStandardIds, x.Id)).Select(x => x.Id))).ToHashSet(), _ => []
		};
		var options = (await registry.GetOptionsAsync(key, parent, year)).Where(x => Guid.TryParse(x.Value, out var id) && allowedIds.Contains(id)).ToList();
		// Resolve reviewed proposals even when their parent differs from today's parent.
		var knownLabels = key switch
		{
			"EducationBoards" => permitted.Boards, "EducationMediums" => permitted.Mediums,
			"StandardsByMediumStage" => permitted.Standards, _ => []
		};
		var labels = knownLabels.Where(x => selected.Contains(x.Id)).Select(x => new AdmissionFormOptionDto
		{ Value = x.Id.ToString(), Label = x.Name, IsActive = allowedIds.Contains(x.Id) }).ToList();
		foreach (var missing in selected.Where(id => !labels.Any(x => x.Value == id.ToString())))
			labels.Add(new() { Value = missing.ToString(), Label = "Option unavailable", IsActive = false });
		if (!available) return labels;
		if (!string.IsNullOrWhiteSpace(search)) options = options.Where(x => x.Label.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
		return Merge(options, labels);
	}

	private async Task<List<AdmissionFormOptionDto>> SiblingLabelsAsync(IReadOnlyCollection<Guid> ids)
	{
		if (ids.Count == 0) return [];
		var query = await SiblingQueryAsync();
		var selected = ids.ToArray();
		var rows = await AsyncExecuter.ToListAsync(query.Where(x => Enumerable.Contains(selected, x.Id))
			.Select(x => new { x.Id, x.FirstName, x.LastName, x.GrNumber, x.AcademicYearId }));
		var roles = await access.GetDatabaseRoleNamesAsync(); var visibleYears = new HashSet<Guid?>();
		foreach (var year in rows.Select(x => x.AcademicYearId).Distinct())
			if (!(await policies.ResolveAsync(year)).Policy.IsMasked("Identity", roles)) visibleYears.Add(year);
		return selected.Select(id =>
		{
			var row = rows.FirstOrDefault(x => x.Id == id); var visible = row != null && visibleYears.Contains(row.AcademicYearId);
			return new AdmissionFormOptionDto { Value = id.ToString(), IsActive = visible,
				Label = row == null ? "Student unavailable" : !visible ? "Restricted student" : $"{row.FirstName} {row.LastName}".Trim() + (string.IsNullOrWhiteSpace(row.GrNumber) ? "" : $" ({row.GrNumber})") };
		}).ToList();
	}
	private async Task<IQueryable<Student>> SiblingQueryAsync()
	{
		var eligible = await access.GetOnboardingEligibleStudentIdsAsync();
		var approved = await overrides.GetQueryableAsync();
		var query = await scope.ApplyStudentScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
		return query.Where(x =>
			(x.Status == StudentLifeCycleStatusEnum.Enrolled || x.Status == StudentLifeCycleStatusEnum.InActive || x.Status == StudentLifeCycleStatusEnum.Left) &&
			(eligible.Contains(x.Id) || approved.Any(o => o.TenantId == CurrentTenant.Id && o.StudentId == x.Id && o.Status == "Approved")));
	}
	private static List<AdmissionFormOptionDto> Merge(List<AdmissionFormOptionDto> first, List<AdmissionFormOptionDto> second) => first.Concat(second)
		.GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase).Select(x => x.OrderByDescending(o => o.IsActive).First()).ToList();
	private static int RowCount(JsonElement value) => value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;
	private static JsonElement Row(JsonElement value, bool repeated, int index) => repeated ? value.ValueKind == JsonValueKind.Array && index < value.GetArrayLength() ? value[index] : default : value;
	private static Guid? Parent(Dictionary<string, JsonElement> values, string? code, bool repeated, int index) => code != null &&
		Guid.TryParse(StudentRecordCustomDetailsService.Unwrap(Row(values.GetValueOrDefault(code), repeated && values.GetValueOrDefault(code).ValueKind == JsonValueKind.Array, index)), out var id) ? id : null;
	private static IEnumerable<Guid> Selected(JsonElement value)
	{
		if (value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().SelectMany(Selected);
		return value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) && id != Guid.Empty ? [id] : [];
	}
}
