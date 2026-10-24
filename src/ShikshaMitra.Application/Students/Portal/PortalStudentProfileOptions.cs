using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.Portal;

/// <summary>Resolves only saved choices; it never exposes staff searches or unrelated student names.</summary>
[Volo.Abp.RemoteService(false)]
public class PortalStudentProfileOptions(AdmissionFormDataSourceRegistry registry, StudentPortalAccessService access)
	: ApplicationService, ITransientDependency
{
	internal async Task PopulateAsync(StudentRecordRegistrationSourceDto source)
	{
		var linked = await access.GetMyStudentsAsync();
		foreach (var section in source.Definition!.Sections)
			foreach (var field in section.Fields)
			{
				if (source.FormMaskedFieldCodes.Contains(field.Code) ||
					!AdmissionFormMasterDataValidator.TrySource(field.OptionsJson, out var key, out var parentField) || !AdmissionFormDataSourceRegistry.IsAllowed(key)) continue;
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
						var choices = ids.Count == 0 ? [] : await registry.GetOptionsAsync(key, parent, source.AcademicYearId);
						resolved = ids.Select(id => choices.FirstOrDefault(c => c.Value == id.ToString()) ??
							new AdmissionFormOptionDto { Value = id.ToString(), Label = "Option unavailable", IsActive = false }).ToList();
					}
					if (section.IsRepeatable) source.OptionsByFieldPath[$"{field.Code}[{i}]"] = resolved;
					all.AddRange(resolved);
				}
				source.OptionsByField[field.Code] = all.DistinctBy(o => o.Value).ToList();
			}
	}
	private static JsonElement Row(JsonElement value, bool repeated, int index) => repeated
		? value.ValueKind == JsonValueKind.Array && index < value.GetArrayLength() ? value[index] : default : value;
	private static IEnumerable<Guid> Selected(JsonElement value) => value.ValueKind == JsonValueKind.Array
		? value.EnumerateArray().SelectMany(Selected) : value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) ? [id] : [];
}
