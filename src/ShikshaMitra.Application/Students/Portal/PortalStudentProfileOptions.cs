using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.Portal;

/// <summary>Resolves only saved choices; it never exposes staff searches or unrelated student names.</summary>
[Volo.Abp.RemoteService(false)]
public class PortalStudentProfileOptions(AdmissionFormDataSourceRegistry registry, StudentPortalAccessService access,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions)
	: ApplicationService, ITransientDependency
{
	internal async Task PopulateAsync(StudentRecordRegistrationSourceDto source)
	{
		var linked = await access.GetMyStudentsAsync();
		foreach (var section in source.Definition!.Sections)
			foreach (var field in section.Fields)
			{
				if (source.FormMaskedFieldCodes.Contains(field.Code) ||
					!AdmissionFormMasterDataValidator.TrySource(AdmissionAcademicFormOptions.OptionsJson(field.Code, field.FieldType, field.OptionsJson), out var key, out var parentField) || !AdmissionFormDataSourceRegistry.IsAllowed(key)) continue;
				var value = source.CurrentValues.GetValueOrDefault(field.Code);
				var count = section.IsRepeatable && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 1;
				var all = new List<AdmissionFormOptionDto>();
				for (var i = 0; i < count; i++)
				{
					var ids = Selected(Row(value, section.IsRepeatable, i)).Distinct().ToList();
					List<AdmissionFormOptionDto> resolved;
					if (key.Equals("StudentSiblings", StringComparison.OrdinalIgnoreCase))
						resolved = ids.Select(id => new AdmissionFormOptionDto { Value = id.ToString(),
							Label = linked.FirstOrDefault(s => s.StudentId == id)?.StudentName ?? "Student unavailable" }).ToList();
					else
					{
						var parentValue = parentField == null ? default : source.CurrentValues.GetValueOrDefault(parentField);
						var parent = Guid.TryParse(StudentRecordCustomDetailsService.Unwrap(Row(parentValue, section.IsRepeatable && parentValue.ValueKind == JsonValueKind.Array, i)), out var parentId) ? (Guid?)parentId : null;
						var choices = ids.Count == 0 ? [] : AdmissionAcademicFormOptions.IsAcademic(key)
							? await SavedAcademicLabelsAsync(key, ids, source.AcademicYearId) : await registry.GetOptionsAsync(key, parent, source.AcademicYearId);
						resolved = ids.Select(id => choices.FirstOrDefault(c => c.Value == id.ToString()) ??
							new AdmissionFormOptionDto { Value = id.ToString(), Label = "Option unavailable", IsActive = false }).ToList();
					}
					if (section.IsRepeatable) source.OptionsByFieldPath[$"{field.Code}[{i}]"] = resolved;
					all.AddRange(resolved);
				}
				source.OptionsByField[field.Code] = all.DistinctBy(o => o.Value).ToList();
			}
	}
	// The calling portal read already authorizes the linked student. Resolve saved IDs only;
	// a parent account must never need staff scope or receive an academic option catalogue.
	private async Task<List<AdmissionFormOptionDto>> SavedAcademicLabelsAsync(string key, List<Guid> selected, Guid year)
	{
		var ids = selected.ToArray();
		if (key.Equals("DivisionsByStandard", StringComparison.OrdinalIgnoreCase))
			return await AsyncExecuter.ToListAsync((await divisions.GetQueryableAsync()).Where(item =>
				item.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, item.Id) &&
				item.Standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId == year)
				.Select(item => new AdmissionFormOptionDto { Value = item.Id.ToString(), Label = item.Name }));
		return await AsyncExecuter.ToListAsync((await standards.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, item.Id) &&
			item.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId == year)
			.Select(item => new AdmissionFormOptionDto { Value = item.Id.ToString(), Label = item.Name }));
	}
	private static JsonElement Row(JsonElement value, bool repeated, int index) => repeated
		? value.ValueKind == JsonValueKind.Array && index < value.GetArrayLength() ? value[index] : default : value;
	private static IEnumerable<Guid> Selected(JsonElement value) => value.ValueKind == JsonValueKind.Array
		? value.EnumerateArray().SelectMany(Selected) : value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) ? [id] : [];
}
